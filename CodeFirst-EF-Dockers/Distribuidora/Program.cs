using System.Data.Common;
using System.Globalization;
using System.Text;
using Distribuidora.Data;
using Distribuidora.Data.Entities;
using Distribuidora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ============================================================
// Configuración de consola y cultura
// ============================================================
Console.OutputEncoding = Encoding.UTF8;
var cultura = CultureInfo.GetCultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;
CultureInfo.CurrentCulture = cultura;
CultureInfo.CurrentUICulture = cultura;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ============================================================
// Host genérico: configuración (appsettings.json, appsettings.{Entorno}.json,
// user-secrets en Development, variables de entorno, argumentos), logging e inyección de dependencias
// ============================================================
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

builder.Services.Configure<StartupOptions>(builder.Configuration.GetSection(StartupOptions.SectionName));
builder.Services.AddDistribuidoraPersistence(builder.Configuration);

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Distribuidora");
var opciones = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;

logger.LogInformation("Entorno: {Entorno}", builder.Environment.EnvironmentName);

try
{
    // --------------------------------------------------------
    // Arranque: migraciones + seed
    // --------------------------------------------------------
    await using (var scope = host.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DistribuidoraContext>();

        if (opciones.ApplyMigrations)
        {
            await AplicarMigracionesAsync(db, opciones, logger, cts.Token);
        }

        if (opciones.SeedData)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            await seeder.SeedAsync(cts.Token);
        }
    }

    // --------------------------------------------------------
    // Consultas demostrativas (contexto nuevo, sin estado del seed)
    // --------------------------------------------------------
    if (opciones.RunDemoQueries)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DistribuidoraContext>();

        // Pausa breve para que el logger asíncrono vacíe sus mensajes antes de imprimir tablas.
        await Task.Delay(200, cts.Token);

        await ConsultasListadoGeneralAsync(db, cts.Token);
        await ConsultasFiltrosYOrdenamientoAsync(db, cts.Token);
        await ConsultasRelacionalesAsync(db, cts.Token);
        await ConsultasAgregacionesAsync(db, cts.Token);

        Console.WriteLine();
        ConsoleTable.Escribir("✔ Consultas demostrativas finalizadas.", ConsoleColor.Green);
    }

    return 0;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Ejecución cancelada por el usuario.");
    return 130;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "La aplicación terminó por un error no controlado.");
    return 1;
}

// ============================================================
// Migraciones con reintentos (SQL Server puede tardar en aceptar conexiones)
// ============================================================
static async Task AplicarMigracionesAsync(
    DistribuidoraContext db, StartupOptions opciones, ILogger logger, CancellationToken ct)
{
    var maxIntentos = Math.Max(1, opciones.MaxStartupRetries);

    for (var intento = 1; ; intento++)
    {
        try
        {
            var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

            if (pendientes.Count == 0)
            {
                logger.LogInformation("Esquema al día: no hay migraciones pendientes.");
                return;
            }

            logger.LogInformation("Aplicando {Cantidad} migración(es): {Migraciones}",
                pendientes.Count, string.Join(", ", pendientes));

            // Equivale a context.Database.Migrate(): crea la base si no existe y aplica lo pendiente.
            await db.Database.MigrateAsync(ct);

            logger.LogInformation("Migraciones aplicadas correctamente.");
            return;
        }
        catch (Exception ex) when (intento < maxIntentos && EsErrorDeConexion(ex))
        {
            logger.LogWarning("SQL Server aún no responde (intento {Intento}/{Max}): {Mensaje}. Reintentando en {Segundos} s…",
                intento, maxIntentos, ex.GetBaseException().Message, opciones.RetryDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(opciones.RetryDelaySeconds), ct);
        }
    }

    static bool EsErrorDeConexion(Exception ex) =>
        ex is DbException or RetryLimitExceededException or TimeoutException
        || ex.InnerException is DbException;
}

// ============================================================
// 1. Listado general de registros por entidad
// ============================================================
static async Task ConsultasListadoGeneralAsync(DistribuidoraContext db, CancellationToken ct)
{
    ConsoleTable.Seccion("1. LISTADO GENERAL DE REGISTROS POR ENTIDAD");

    var categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.IdCategoria).ToListAsync(ct);
    ConsoleTable.De(categorias)
        .Col("ID", c => c.IdCategoria, Alineacion.Derecha)
        .Col("Nombre", c => c.Nombre)
        .Col("Descripción", c => c.Descripcion)
        .Imprimir("Categorías");

    var proveedores = await db.Proveedores.AsNoTracking().OrderBy(p => p.IdProveedor).ToListAsync(ct);
    ConsoleTable.De(proveedores)
        .Col("ID", p => p.IdProveedor, Alineacion.Derecha)
        .Col("Nombre", p => p.Nombre)
        .Col("Teléfono", p => p.Telefono)
        .Col("Email", p => p.Email)
        .Imprimir("Proveedores");

    var productos = await db.Productos.AsNoTracking().OrderBy(p => p.IdProducto).ToListAsync(ct);
    ConsoleTable.De(productos)
        .Col("ID", p => p.IdProducto, Alineacion.Derecha)
        .Col("Nombre", p => p.Nombre)
        .Col("Precio", p => p.Precio.ToString("C"), Alineacion.Derecha)
        .Col("Stock", p => p.Stock, Alineacion.Derecha)
        .Col("ID Cat.", p => p.IdCategoria, Alineacion.Derecha)
        .Col("ID Prov.", p => p.IdProveedor, Alineacion.Derecha)
        .Imprimir("Productos");

    var clientes = await db.Clientes.AsNoTracking().OrderBy(c => c.IdCliente).ToListAsync(ct);
    ConsoleTable.De(clientes)
        .Col("ID", c => c.IdCliente, Alineacion.Derecha)
        .Col("Nombre", c => c.Nombre)
        .Col("Email", c => c.Email)
        .Col("Teléfono", c => c.Telefono)
        .Col("Dirección", c => c.Direccion)
        .Imprimir("Clientes");

    var empleados = await db.Empleados.AsNoTracking().OrderBy(e => e.IdEmpleado).ToListAsync(ct);
    ConsoleTable.De(empleados)
        .Col("ID", e => e.IdEmpleado, Alineacion.Derecha)
        .Col("Nombre", e => e.Nombre)
        .Col("Puesto", e => e.Puesto)
        .Col("Contratación", e => e.FechaContratacion.ToString("dd/MM/yyyy"))
        .Imprimir("Empleados");

    var pedidos = await db.Pedidos.AsNoTracking().OrderBy(p => p.IdPedido).ToListAsync(ct);
    ConsoleTable.De(pedidos)
        .Col("ID", p => p.IdPedido, Alineacion.Derecha)
        .Col("Fecha", p => p.FechaPedido.ToString("dd/MM/yyyy"))
        .Col("Estado", p => p.Estado)
        .Col("ID Cliente", p => p.IdCliente, Alineacion.Derecha)
        .Col("ID Empleado", p => p.IdEmpleado, Alineacion.Derecha)
        .Imprimir("Pedidos");

    var detalles = await db.DetallePedidos.AsNoTracking().OrderBy(d => d.IdDetalle).ToListAsync(ct);
    ConsoleTable.De(detalles)
        .Col("ID", d => d.IdDetalle, Alineacion.Derecha)
        .Col("ID Pedido", d => d.IdPedido, Alineacion.Derecha)
        .Col("ID Producto", d => d.IdProducto, Alineacion.Derecha)
        .Col("Cantidad", d => d.Cantidad, Alineacion.Derecha)
        .Col("Precio unit.", d => d.PrecioUnitario.ToString("C"), Alineacion.Derecha)
        .Imprimir("DetallePedidos");
}

// ============================================================
// 2. Filtros y ordenamiento (Where, OrderBy, ThenBy, Take)
// ============================================================
static async Task ConsultasFiltrosYOrdenamientoAsync(DistribuidoraContext db, CancellationToken ct)
{
    ConsoleTable.Seccion("2. FILTROS Y ORDENAMIENTO");

    var productosCaros = await db.Productos.AsNoTracking()
        .Where(p => p.Precio >= 40m)
        .OrderByDescending(p => p.Precio)
        .ThenBy(p => p.Nombre)
        .ToListAsync(ct);
    ConsoleTable.De(productosCaros)
        .Col("Producto", p => p.Nombre)
        .Col("Precio", p => p.Precio.ToString("C"), Alineacion.Derecha)
        .Imprimir("Productos con precio ≥ $40.00, del más caro al más barato");

    var pendientes = await db.Pedidos.AsNoTracking()
        .Where(p => p.Estado == EstadoPedido.Pendiente)
        .OrderBy(p => p.FechaPedido)
        .ToListAsync(ct);
    ConsoleTable.De(pendientes)
        .Col("ID", p => p.IdPedido, Alineacion.Derecha)
        .Col("Fecha", p => p.FechaPedido.ToString("dd/MM/yyyy"))
        .Col("Estado", p => p.Estado)
        .Imprimir("Pedidos pendientes, del más antiguo al más reciente");

    var corte = new DateOnly(2023, 1, 1);
    var veteranos = await db.Empleados.AsNoTracking()
        .Where(e => e.FechaContratacion < corte)
        .OrderBy(e => e.FechaContratacion)
        .ToListAsync(ct);
    ConsoleTable.De(veteranos)
        .Col("Empleado", e => e.Nombre)
        .Col("Puesto", e => e.Puesto)
        .Col("Contratación", e => e.FechaContratacion.ToString("dd/MM/yyyy"))
        .Imprimir("Empleados contratados antes de 2023, por antigüedad");

    var stockBajo = await db.Productos.AsNoTracking()
        .Where(p => p.Stock < 50)
        .OrderBy(p => p.Stock)
        .ToListAsync(ct);
    ConsoleTable.De(stockBajo)
        .Col("Producto", p => p.Nombre)
        .Col("Stock", p => p.Stock, Alineacion.Derecha)
        .Imprimir("Productos con stock menor a 50 unidades");

    var top5 = await db.Productos.AsNoTracking()
        .OrderByDescending(p => p.Stock)
        .Take(5)
        .Select(p => new { p.Nombre, p.Stock })
        .ToListAsync(ct);
    ConsoleTable.De(top5)
        .Col("Producto", p => p.Nombre)
        .Col("Stock", p => p.Stock, Alineacion.Derecha)
        .Imprimir("Top 5 productos con mayor stock (equivale a SELECT TOP 5 … ORDER BY stock DESC)");
}

// ============================================================
// 3. Consultas relacionales (Include / ThenInclude, proyecciones Select, carga explícita)
// ============================================================
static async Task ConsultasRelacionalesAsync(DistribuidoraContext db, CancellationToken ct)
{
    ConsoleTable.Seccion("3. CONSULTAS RELACIONALES");

    // Carga ansiosa (eager loading) de dos relaciones N:1.
    var productos = await db.Productos.AsNoTracking()
        .Include(p => p.Categoria)
        .Include(p => p.Proveedor)
        .OrderBy(p => p.Categoria.Nombre)
        .ThenBy(p => p.Nombre)
        .ToListAsync(ct);
    ConsoleTable.De(productos)
        .Col("Producto", p => p.Nombre)
        .Col("Categoría", p => p.Categoria.Nombre)
        .Col("Proveedor", p => p.Proveedor.Nombre)
        .Imprimir("Productos con su categoría y proveedor (Include)");

    // Proyección: EF genera los JOIN y solo trae las columnas necesarias.
    var pedidosResumen = await db.Pedidos.AsNoTracking()
        .OrderBy(p => p.IdPedido)
        .Select(p => new
        {
            p.IdPedido,
            p.FechaPedido,
            Cliente = p.Cliente.Nombre,
            Empleado = p.Empleado.Nombre,
            p.Estado,
            Articulos = p.Detalles.Sum(d => d.Cantidad),
            Total = p.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
        })
        .ToListAsync(ct);
    ConsoleTable.De(pedidosResumen)
        .Col("Pedido", p => p.IdPedido, Alineacion.Derecha)
        .Col("Fecha", p => p.FechaPedido.ToString("dd/MM/yyyy"))
        .Col("Cliente", p => p.Cliente)
        .Col("Atendió", p => p.Empleado)
        .Col("Estado", p => p.Estado)
        .Col("Artículos", p => p.Articulos, Alineacion.Derecha)
        .Col("Total", p => p.Total.ToString("C"), Alineacion.Derecha)
        .Imprimir("Pedidos con cliente, empleado y total (proyección con Select)");

    // Include + ThenInclude a través de la tabla intermedia N:M.
    var pedidosDetallados = await db.Pedidos.AsNoTracking()
        .Include(p => p.Cliente)
        .Include(p => p.Detalles)
            .ThenInclude(d => d.Producto)
        .OrderBy(p => p.IdPedido)
        .ToListAsync(ct);

    ConsoleTable.Subtitulo("Detalle de cada pedido (Include → ThenInclude por la tabla N:M)");
    foreach (var pedido in pedidosDetallados)
    {
        var total = pedido.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        Console.WriteLine($"  Pedido #{pedido.IdPedido} · {pedido.FechaPedido:dd/MM/yyyy} · {pedido.Cliente.Nombre} · {pedido.Estado}");
        foreach (var d in pedido.Detalles.OrderBy(d => d.Producto.Nombre))
        {
            Console.WriteLine($"     ├─ {d.Producto.Nombre,-24} {d.Cantidad,4} × {d.PrecioUnitario,10:C} = {d.Cantidad * d.PrecioUnitario,11:C}");
        }
        Console.WriteLine($"     └─ {"Total del pedido",-24} {"",4}   {"",10}   {total,11:C}");
    }

    // Carga explícita (explicit loading): la colección se carga bajo demanda sobre una entidad rastreada.
    var cliente = await db.Clientes
        .OrderBy(c => c.IdCliente)
        .FirstOrDefaultAsync(ct);

    if (cliente is not null)
    {
        await db.Entry(cliente)
            .Collection(c => c.Pedidos)
            .Query()
            .OrderBy(p => p.FechaPedido)
            .LoadAsync(ct);

        ConsoleTable.De(cliente.Pedidos)
            .Col("Pedido", p => p.IdPedido, Alineacion.Derecha)
            .Col("Fecha", p => p.FechaPedido.ToString("dd/MM/yyyy"))
            .Col("Estado", p => p.Estado)
            .Imprimir($"Pedidos de '{cliente.Nombre}' (carga explícita con Entry().Collection().LoadAsync())");
    }
}

// ============================================================
// 4. Agregaciones y agrupaciones (Count, Sum, Average, GroupBy)
// ============================================================
static async Task ConsultasAgregacionesAsync(DistribuidoraContext db, CancellationToken ct)
{
    ConsoleTable.Seccion("4. AGREGACIONES Y AGRUPACIONES");

    var conteos = new[]
    {
        new { Tabla = "Categorias", Registros = await db.Categorias.CountAsync(ct) },
        new { Tabla = "Proveedores", Registros = await db.Proveedores.CountAsync(ct) },
        new { Tabla = "Productos", Registros = await db.Productos.CountAsync(ct) },
        new { Tabla = "Clientes", Registros = await db.Clientes.CountAsync(ct) },
        new { Tabla = "Empleados", Registros = await db.Empleados.CountAsync(ct) },
        new { Tabla = "Pedidos", Registros = await db.Pedidos.CountAsync(ct) },
        new { Tabla = "DetallePedidos", Registros = await db.DetallePedidos.CountAsync(ct) },
    };
    ConsoleTable.De(conteos)
        .Col("Tabla", c => c.Tabla)
        .Col("Registros", c => c.Registros, Alineacion.Derecha)
        .Imprimir("Número de registros por tabla (Count)");

    var porCategoria = await db.Productos.AsNoTracking()
        .GroupBy(p => p.Categoria.Nombre)
        .Select(g => new
        {
            Categoria = g.Key,
            Productos = g.Count(),
            StockTotal = g.Sum(p => p.Stock),
            PrecioPromedio = g.Average(p => p.Precio),
            ValorInventario = g.Sum(p => p.Precio * p.Stock)
        })
        .OrderByDescending(x => x.ValorInventario)
        .ToListAsync(ct);
    ConsoleTable.De(porCategoria)
        .Col("Categoría", x => x.Categoria)
        .Col("Productos", x => x.Productos, Alineacion.Derecha)
        .Col("Stock total", x => x.StockTotal, Alineacion.Derecha)
        .Col("Precio prom.", x => x.PrecioPromedio.ToString("C"), Alineacion.Derecha)
        .Col("Valor inventario", x => x.ValorInventario.ToString("C"), Alineacion.Derecha)
        .Imprimir("Inventario por categoría (GroupBy + Count/Sum/Average)");

    var porEstado = await db.Pedidos.AsNoTracking()
        .GroupBy(p => p.Estado)
        .Select(g => new { Estado = g.Key, Pedidos = g.Count() })
        .OrderByDescending(x => x.Pedidos)
        .ToListAsync(ct);
    ConsoleTable.De(porEstado)
        .Col("Estado", x => x.Estado)
        .Col("Pedidos", x => x.Pedidos, Alineacion.Derecha)
        .Imprimir("Pedidos por estado (GroupBy)");

    var totalPorPedido = await db.DetallePedidos.AsNoTracking()
        .GroupBy(d => d.IdPedido)
        .Select(g => new
        {
            IdPedido = g.Key,
            Partidas = g.Count(),
            Total = g.Sum(d => d.Cantidad * d.PrecioUnitario)
        })
        .OrderBy(x => x.IdPedido)
        .ToListAsync(ct);
    ConsoleTable.De(totalPorPedido)
        .Col("Pedido", x => x.IdPedido, Alineacion.Derecha)
        .Col("Partidas", x => x.Partidas, Alineacion.Derecha)
        .Col("Total", x => x.Total.ToString("C"), Alineacion.Derecha)
        .Imprimir("Total vendido por pedido (equivale a SUM(cantidad * precio_unitario) … GROUP BY id_pedido)");

    var ventasPorEmpleado = await db.DetallePedidos.AsNoTracking()
        .Where(d => d.Pedido.Estado != EstadoPedido.Cancelado)
        .GroupBy(d => d.Pedido.Empleado.Nombre)
        .Select(g => new
        {
            Empleado = g.Key,
            Pedidos = g.Select(d => d.IdPedido).Distinct().Count(),
            Importe = g.Sum(d => d.Cantidad * d.PrecioUnitario)
        })
        .OrderByDescending(x => x.Importe)
        .ToListAsync(ct);
    ConsoleTable.De(ventasPorEmpleado)
        .Col("Empleado", x => x.Empleado)
        .Col("Pedidos", x => x.Pedidos, Alineacion.Derecha)
        .Col("Importe vendido", x => x.Importe.ToString("C"), Alineacion.Derecha)
        .Imprimir("Ventas por empleado, excluyendo cancelados (GroupBy por navegación)");

    var masVendidos = await db.DetallePedidos.AsNoTracking()
        .GroupBy(d => d.Producto.Nombre)
        .Select(g => new { Producto = g.Key, Unidades = g.Sum(d => d.Cantidad) })
        .OrderByDescending(x => x.Unidades)
        .ThenBy(x => x.Producto)
        .ToListAsync(ct);
    ConsoleTable.De(masVendidos)
        .Col("Producto", x => x.Producto)
        .Col("Unidades vendidas", x => x.Unidades, Alineacion.Derecha)
        .Imprimir("Productos más vendidos (GroupBy + Sum)");

    var granTotal = await db.DetallePedidos.AsNoTracking()
        .Where(d => d.Pedido.Estado != EstadoPedido.Cancelado)
        .SumAsync(d => d.Cantidad * d.PrecioUnitario, ct);
    Console.WriteLine();
    ConsoleTable.Escribir($"  Importe total vendido (sin cancelados): {granTotal:C}", ConsoleColor.Green);
}
