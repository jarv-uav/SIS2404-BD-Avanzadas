namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Categorias</c>. Una categoría agrupa N productos.</summary>
public class Categoria
{
    public int IdCategoria { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
