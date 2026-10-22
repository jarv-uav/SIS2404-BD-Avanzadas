namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Productos</c>. Relación N:1 con Categorias y N:1 con Proveedores.</summary>
public class Producto
{
    public int IdProducto { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public int Stock { get; set; }

    public int IdCategoria { get; set; }

    public int IdProveedor { get; set; }

    public Categoria Categoria { get; set; } = null!;

    public Proveedor Proveedor { get; set; } = null!;

    public ICollection<DetallePedido> DetallesPedido { get; set; } = new List<DetallePedido>();
}
