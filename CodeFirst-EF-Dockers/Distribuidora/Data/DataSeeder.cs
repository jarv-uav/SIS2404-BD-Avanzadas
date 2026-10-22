using Distribuidora.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Distribuidora.Data;

/// <summary>
/// Sembrado idempotente con los mismos datos dummy del script SQL.
/// Cada tabla se puebla solo si está vacía (<c>!context.Set&lt;T&gt;().Any()</c>), y las llaves
/// foráneas se resuelven por llaves naturales (nombre / email), de modo que el seeder funciona
/// aunque algunas tablas ya tengan datos y no depende de los valores IDENTITY generados.
/// Todo se ejecuta en una transacción dentro de la estrategia de reintentos de EF Core.
/// </summary>
public sealed class DataSeeder(DistribuidoraContext context, ILogger<DataSeeder> logger)
{
    // ------------------------------------------------------------
    // Datos semilla (idénticos al script distribuidora_sqlserver.sql)
    // ------------------------------------------------------------
    private static readonly Categoria[] SemillaCategorias =
    [
        new() { Nombre = "Electrónica", Descripcion = "Dispositivos y accesorios electrónicos" },
        new() { Nombre = "Papelería", Descripcion = "Artículos de oficina y escuela" },
        new() { Nombre = "Limpieza", Descripcion = "Productos de limpieza para hogar y oficina" },
    ];

    private static readonly Proveedor[] SemillaProveedores =
    [
        new() { Nombre = "TecnoSuministros SA", Telefono = "2281234567", Email = "contacto@tecnosuministros.com" },
        new() { Nombre = "Distribuidora Papel Fino", Telefono = "2287654321", Email = "ventas@papelfino.com" },
        new() { Nombre = "Químicos del Golfo", Telefono = "2289876543", Email = "info@quimicosgolfo.com" },
    ];

    private sealed record SemillaProducto(string Nombre, decimal Precio, int Stock, string Categoria, string Proveedor);

    private static readonly SemillaProducto[] SemillaProductos =
    [
        new("Mouse inalámbrico", 250.00m, 40, "Electrónica", "TecnoSuministros SA"),
        new("Teclado USB", 350.00m, 25, "Electrónica", "TecnoSuministros SA"),
        new("Cuaderno profesional", 45.50m, 100, "Papelería", "Distribuidora Papel Fino"),
        new("Paquete de bolígrafos", 30.00m, 150, "Papelería", "Distribuidora Papel Fino"),
        new("Cloro 1L", 22.00m, 80, "Limpieza", "Químicos del Golfo"),
        new("Jabón líquido 1L", 35.00m, 60, "Limpieza", "Químicos del Golfo"),
    ];

    private static readonly Cliente[] SemillaClientes =
    [
        new() { Nombre = "Comercial López", Email = "contacto@comerciallopez.com", Telefono = "2281112233", Direccion = "Av. Reforma 123, Córdoba" },
        new() { Nombre = "Papelería Central", Email = "ventas@papeleriacentral.com", Telefono = "2282223344", Direccion = "Calle Juárez 45, Orizaba" },
        new() { Nombre = "Hotel Bugambilias", Email = "compras@hotelbugambilias.com", Telefono = "2283334455", Direccion = "Blvd. Costero 78, Boca del Río" },
    ];

    private static readonly Empleado[] SemillaEmpleados =
    [
        new() { Nombre = "Ana Martínez", Puesto = "Vendedor", FechaContratacion = new DateOnly(2022, 3, 15) },
        new() { Nombre = "Luis Hernández", Puesto = "Vendedor", FechaContratacion = new DateOnly(2023, 6, 1) },
        new() { Nombre = "Carla Ramírez", Puesto = "Supervisor", FechaContratacion = new DateOnly(2021, 1, 10) },
    ];

    /// <summary>Un pedido se identifica de forma natural por (fecha, email del cliente).</summary>
    private sealed record SemillaPedido(DateOnly Fecha, EstadoPedido Estado, string ClienteEmail, string Empleado);

    private static readonly SemillaPedido[] SemillaPedidos =
    [
        new(new DateOnly(2026, 8, 1), EstadoPedido.Entregado, "contacto@comerciallopez.com", "Ana Martínez"),
        new(new DateOnly(2026, 8, 10), EstadoPedido.Enviado, "ventas@papeleriacentral.com", "Luis Hernández"),
        new(new DateOnly(2026, 8, 20), EstadoPedido.Pendiente, "compras@hotelbugambilias.com", "Ana Martínez"),
        new(new DateOnly(2026, 8, 25), EstadoPedido.Pendiente, "contacto@comerciallopez.com", "Carla Ramírez"),
    ];

    private sealed record SemillaDetalle(DateOnly FechaPedido, string ClienteEmail, string Producto, int Cantidad, decimal PrecioUnitario);

    private static readonly SemillaDetalle[] SemillaDetalles =
    [
        new(new DateOnly(2026, 8, 1), "contacto@comerciallopez.com", "Mouse inalámbrico", 2, 250.00m),
        new(new DateOnly(2026, 8, 1), "contacto@comerciallopez.com", "Cuaderno profesional", 5, 45.50m),
        new(new DateOnly(2026, 8, 10), "ventas@papeleriacentral.com", "Paquete de bolígrafos", 10, 30.00m),
        new(new DateOnly(2026, 8, 10), "ventas@papeleriacentral.com", "Cloro 1L", 3, 22.00m),
        new(new DateOnly(2026, 8, 20), "compras@hotelbugambilias.com", "Teclado USB", 1, 350.00m),
        new(new DateOnly(2026, 8, 25), "contacto@comerciallopez.com", "Jabón líquido 1L", 4, 35.00m),
        new(new DateOnly(2026, 8, 25), "contacto@comerciallopez.com", "Cuaderno profesional", 2, 45.50m),
    ];

    // ------------------------------------------------------------
    // Punto de entrada
    // ------------------------------------------------------------
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Si la estrategia reintenta, se descarta lo que haya quedado del intento fallido.
            context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // El orden respeta las dependencias de llaves foráneas.
            await SembrarSiVaciaAsync(_ => Task.FromResult(Clonar(SemillaCategorias, CopiarCategoria)), cancellationToken);
            await SembrarSiVaciaAsync(_ => Task.FromResult(Clonar(SemillaProveedores, CopiarProveedor)), cancellationToken);
            await SembrarSiVaciaAsync(CrearProductosAsync, cancellationToken);
            await SembrarSiVaciaAsync(_ => Task.FromResult(Clonar(SemillaClientes, CopiarCliente)), cancellationToken);
            await SembrarSiVaciaAsync(_ => Task.FromResult(Clonar(SemillaEmpleados, CopiarEmpleado)), cancellationToken);
            await SembrarSiVaciaAsync(CrearPedidosAsync, cancellationToken);
            await SembrarSiVaciaAsync(CrearDetallesAsync, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        context.ChangeTracker.Clear();
    }

    private async Task SembrarSiVaciaAsync<T>(
        Func<CancellationToken, Task<IReadOnlyList<T>>> crearRegistros,
        CancellationToken cancellationToken)
        where T : class
    {
        var tabla = context.Model.FindEntityType(typeof(T))?.GetTableName() ?? typeof(T).Name;

        if (await context.Set<T>().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed: la tabla {Tabla} ya contiene datos; se omite.", tabla);
            return;
        }

        var registros = await crearRegistros(cancellationToken);
        context.Set<T>().AddRange(registros);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seed: {Cantidad} registros insertados en {Tabla}.", registros.Count, tabla);
    }

    // ------------------------------------------------------------
    // Fábricas con resolución de llaves foráneas
    // ------------------------------------------------------------
    private async Task<IReadOnlyList<Producto>> CrearProductosAsync(CancellationToken ct)
    {
        var categorias = await context.Categorias.AsNoTracking()
            .ToDictionaryAsync(c => c.Nombre, c => c.IdCategoria, ct);
        var proveedores = await context.Proveedores.AsNoTracking()
            .ToDictionaryAsync(p => p.Nombre, p => p.IdProveedor, ct);

        return SemillaProductos
            .Select(s => new Producto
            {
                Nombre = s.Nombre,
                Precio = s.Precio,
                Stock = s.Stock,
                IdCategoria = Resolver(categorias, s.Categoria, "la categoría"),
                IdProveedor = Resolver(proveedores, s.Proveedor, "el proveedor"),
            })
            .ToList();
    }

    private async Task<IReadOnlyList<Pedido>> CrearPedidosAsync(CancellationToken ct)
    {
        var clientes = await context.Clientes.AsNoTracking()
            .ToDictionaryAsync(c => c.Email, c => c.IdCliente, ct);
        var empleados = await context.Empleados.AsNoTracking()
            .ToDictionaryAsync(e => e.Nombre, e => e.IdEmpleado, ct);

        return SemillaPedidos
            .Select(s => new Pedido
            {
                FechaPedido = s.Fecha,
                Estado = s.Estado,
                IdCliente = Resolver(clientes, s.ClienteEmail, "el cliente"),
                IdEmpleado = Resolver(empleados, s.Empleado, "el empleado"),
            })
            .ToList();
    }

    private async Task<IReadOnlyList<DetallePedido>> CrearDetallesAsync(CancellationToken ct)
    {
        var pedidosExistentes = await context.Pedidos.AsNoTracking()
            .Select(p => new { p.IdPedido, p.FechaPedido, ClienteEmail = p.Cliente.Email })
            .ToListAsync(ct);

        var pedidos = new Dictionary<(DateOnly, string), int>();
        foreach (var p in pedidosExistentes)
        {
            pedidos.TryAdd((p.FechaPedido, p.ClienteEmail), p.IdPedido);
        }

        var productos = await context.Productos.AsNoTracking()
            .ToDictionaryAsync(p => p.Nombre, p => p.IdProducto, ct);

        return SemillaDetalles
            .Select(s => new DetallePedido
            {
                IdPedido = Resolver(pedidos, (s.FechaPedido, s.ClienteEmail), "el pedido"),
                IdProducto = Resolver(productos, s.Producto, "el producto"),
                Cantidad = s.Cantidad,
                PrecioUnitario = s.PrecioUnitario,
            })
            .ToList();
    }

    // ------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------
    private static int Resolver<TClave>(IReadOnlyDictionary<TClave, int> mapa, TClave clave, string descripcion)
        where TClave : notnull
        => mapa.TryGetValue(clave, out var id)
            ? id
            : throw new InvalidOperationException($"Seed: no se encontró {descripcion} '{clave}'.");

    // Se insertan copias para que las instancias estáticas nunca queden rastreadas por el DbContext.
    private static IReadOnlyList<T> Clonar<T>(IEnumerable<T> origen, Func<T, T> copiar) => origen.Select(copiar).ToList();

    private static Categoria CopiarCategoria(Categoria c) => new() { Nombre = c.Nombre, Descripcion = c.Descripcion };

    private static Proveedor CopiarProveedor(Proveedor p) => new() { Nombre = p.Nombre, Telefono = p.Telefono, Email = p.Email };

    private static Cliente CopiarCliente(Cliente c) => new() { Nombre = c.Nombre, Email = c.Email, Telefono = c.Telefono, Direccion = c.Direccion };

    private static Empleado CopiarEmpleado(Empleado e) => new() { Nombre = e.Nombre, Puesto = e.Puesto, FechaContratacion = e.FechaContratacion };
}
