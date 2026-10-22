using Distribuidora.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Distribuidora.Data;

/// <summary>
/// DbContext de la base "Distribuidora". Todo el mapeo se define con Fluent API
/// dentro de <see cref="OnModelCreating"/>; las entidades son POCOs sin atributos.
/// </summary>
/// <remarks>
/// Notas de fidelidad respecto al script T-SQL:
/// <list type="bullet">
/// <item>Columnas VARCHAR → <c>IsUnicode(false)</c> + <c>HasMaxLength(n)</c>.</item>
/// <item>DECIMAL(10,2) → <c>HasPrecision(10, 2)</c>; DATE → <see cref="DateOnly"/>.</item>
/// <item>ON DELETE NO ACTION → <see cref="DeleteBehavior.NoAction"/>; ON DELETE CASCADE → <see cref="DeleteBehavior.Cascade"/>.</item>
/// <item>ON UPDATE CASCADE no es expresable en el modelo de EF Core (las PK IDENTITY son inmutables);
/// se declara explícitamente en la migración inicial para que el esquema sea idéntico al script.</item>
/// <item>Los UNIQUE del script se mapean como índices únicos con el mismo efecto.</item>
/// </list>
/// </remarks>
public sealed class DistribuidoraContext(DbContextOptions<DistribuidoraContext> options) : DbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallePedidos => Set<DetallePedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurarCategorias(modelBuilder.Entity<Categoria>());
        ConfigurarProveedores(modelBuilder.Entity<Proveedor>());
        ConfigurarProductos(modelBuilder.Entity<Producto>());
        ConfigurarClientes(modelBuilder.Entity<Cliente>());
        ConfigurarEmpleados(modelBuilder.Entity<Empleado>());
        ConfigurarPedidos(modelBuilder.Entity<Pedido>());
        ConfigurarDetallePedidos(modelBuilder.Entity<DetallePedido>());
    }

    // ------------------------------------------------------------
    // Categorias
    // ------------------------------------------------------------
    private static void ConfigurarCategorias(EntityTypeBuilder<Categoria> e)
    {
        e.ToTable("Categorias");
        e.HasKey(c => c.IdCategoria);

        e.Property(c => c.IdCategoria)
            .HasColumnName("id_categoria")
            .UseIdentityColumn(1, 1);

        e.Property(c => c.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        e.Property(c => c.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(150)
            .IsUnicode(false);

        // nombre VARCHAR(50) NOT NULL UNIQUE
        e.HasIndex(c => c.Nombre)
            .IsUnique()
            .HasDatabaseName("uq_categorias_nombre");
    }

    // ------------------------------------------------------------
    // Proveedores
    // ------------------------------------------------------------
    private static void ConfigurarProveedores(EntityTypeBuilder<Proveedor> e)
    {
        e.ToTable("Proveedores");
        e.HasKey(p => p.IdProveedor);

        e.Property(p => p.IdProveedor)
            .HasColumnName("id_proveedor")
            .UseIdentityColumn(1, 1);

        e.Property(p => p.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        e.Property(p => p.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(20)
            .IsUnicode(false);

        e.Property(p => p.Email)
            .HasColumnName("email")
            .HasMaxLength(100)
            .IsUnicode(false);

        // email VARCHAR(100) UNIQUE (nullable). EF Core agregaría por convención el filtro
        // "[email] IS NOT NULL"; se desactiva para replicar el UNIQUE del script (admite un solo NULL).
        e.HasIndex(p => p.Email)
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName("uq_proveedores_email");
    }

    // ------------------------------------------------------------
    // Productos
    // ------------------------------------------------------------
    private static void ConfigurarProductos(EntityTypeBuilder<Producto> e)
    {
        e.ToTable("Productos", t =>
        {
            t.HasCheckConstraint("ck_productos_precio", "[precio] > 0");
            t.HasCheckConstraint("ck_productos_stock", "[stock] >= 0");
        });
        e.HasKey(p => p.IdProducto);

        e.Property(p => p.IdProducto)
            .HasColumnName("id_producto")
            .UseIdentityColumn(1, 1);

        e.Property(p => p.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        e.Property(p => p.Precio)
            .HasColumnName("precio")
            .HasPrecision(10, 2)
            .IsRequired();

        e.Property(p => p.Stock)
            .HasColumnName("stock")
            .HasDefaultValue(0)
            .IsRequired();

        e.Property(p => p.IdCategoria).HasColumnName("id_categoria");
        e.Property(p => p.IdProveedor).HasColumnName("id_proveedor");

        e.HasOne(p => p.Categoria)
            .WithMany(c => c.Productos)
            .HasForeignKey(p => p.IdCategoria)
            .HasConstraintName("fk_producto_categoria")
            .OnDelete(DeleteBehavior.NoAction);

        e.HasOne(p => p.Proveedor)
            .WithMany(pr => pr.Productos)
            .HasForeignKey(p => p.IdProveedor)
            .HasConstraintName("fk_producto_proveedor")
            .OnDelete(DeleteBehavior.NoAction);
    }

    // ------------------------------------------------------------
    // Clientes
    // ------------------------------------------------------------
    private static void ConfigurarClientes(EntityTypeBuilder<Cliente> e)
    {
        e.ToTable("Clientes");
        e.HasKey(c => c.IdCliente);

        e.Property(c => c.IdCliente)
            .HasColumnName("id_cliente")
            .UseIdentityColumn(1, 1);

        e.Property(c => c.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        e.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        e.Property(c => c.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(20)
            .IsUnicode(false);

        e.Property(c => c.Direccion)
            .HasColumnName("direccion")
            .HasMaxLength(150)
            .IsUnicode(false);

        // email VARCHAR(100) NOT NULL UNIQUE
        e.HasIndex(c => c.Email)
            .IsUnique()
            .HasDatabaseName("uq_clientes_email");
    }

    // ------------------------------------------------------------
    // Empleados
    // ------------------------------------------------------------
    private static void ConfigurarEmpleados(EntityTypeBuilder<Empleado> e)
    {
        e.ToTable("Empleados");
        e.HasKey(em => em.IdEmpleado);

        e.Property(em => em.IdEmpleado)
            .HasColumnName("id_empleado")
            .UseIdentityColumn(1, 1);

        e.Property(em => em.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        e.Property(em => em.Puesto)
            .HasColumnName("puesto")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        e.Property(em => em.FechaContratacion)
            .HasColumnName("fecha_contratacion")
            .HasColumnType("date")
            .IsRequired();
    }

    // ------------------------------------------------------------
    // Pedidos
    // ------------------------------------------------------------
    private static void ConfigurarPedidos(EntityTypeBuilder<Pedido> e)
    {
        e.ToTable("Pedidos", t =>
            t.HasCheckConstraint(
                "ck_pedidos_estado",
                "[estado] IN ('Pendiente','Enviado','Entregado','Cancelado')"));
        e.HasKey(p => p.IdPedido);

        e.Property(p => p.IdPedido)
            .HasColumnName("id_pedido")
            .UseIdentityColumn(1, 1);

        // DATE NOT NULL DEFAULT (CAST(GETDATE() AS DATE))
        e.Property(p => p.FechaPedido)
            .HasColumnName("fecha_pedido")
            .HasColumnType("date")
            .HasDefaultValueSql("CAST(GETDATE() AS DATE)")
            .IsRequired();

        // VARCHAR(20) NOT NULL DEFAULT 'Pendiente' + CHECK; el enum se guarda como texto.
        e.Property(p => p.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue(EstadoPedido.Pendiente)
            .IsRequired();

        e.Property(p => p.IdCliente).HasColumnName("id_cliente");
        e.Property(p => p.IdEmpleado).HasColumnName("id_empleado");

        e.HasOne(p => p.Cliente)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.IdCliente)
            .HasConstraintName("fk_pedido_cliente")
            .OnDelete(DeleteBehavior.NoAction);

        e.HasOne(p => p.Empleado)
            .WithMany(em => em.Pedidos)
            .HasForeignKey(p => p.IdEmpleado)
            .HasConstraintName("fk_pedido_empleado")
            .OnDelete(DeleteBehavior.NoAction);
    }

    // ------------------------------------------------------------
    // DetallePedidos (N:M Pedidos ↔ Productos con carga útil)
    // ------------------------------------------------------------
    private static void ConfigurarDetallePedidos(EntityTypeBuilder<DetallePedido> e)
    {
        e.ToTable("DetallePedidos", t =>
        {
            t.HasCheckConstraint("ck_detalle_cantidad", "[cantidad] > 0");
            t.HasCheckConstraint("ck_detalle_precio_unitario", "[precio_unitario] > 0");
        });
        e.HasKey(d => d.IdDetalle);

        e.Property(d => d.IdDetalle)
            .HasColumnName("id_detalle")
            .UseIdentityColumn(1, 1);

        e.Property(d => d.IdPedido).HasColumnName("id_pedido");
        e.Property(d => d.IdProducto).HasColumnName("id_producto");

        e.Property(d => d.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        e.Property(d => d.PrecioUnitario)
            .HasColumnName("precio_unitario")
            .HasPrecision(10, 2)
            .IsRequired();

        // CONSTRAINT uq_pedido_producto UNIQUE (id_pedido, id_producto)
        e.HasIndex(d => new { d.IdPedido, d.IdProducto })
            .IsUnique()
            .HasDatabaseName("uq_pedido_producto");

        e.HasOne(d => d.Pedido)
            .WithMany(p => p.Detalles)
            .HasForeignKey(d => d.IdPedido)
            .HasConstraintName("fk_detalle_pedido")
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(d => d.Producto)
            .WithMany(p => p.DetallesPedido)
            .HasForeignKey(d => d.IdProducto)
            .HasConstraintName("fk_detalle_producto")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
