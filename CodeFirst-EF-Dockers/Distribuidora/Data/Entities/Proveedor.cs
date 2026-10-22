namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Proveedores</c>. Un proveedor surte N productos.</summary>
public class Proveedor
{
    public int IdProveedor { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
