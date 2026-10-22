namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Empleados</c>. Un empleado atiende N pedidos.</summary>
public class Empleado
{
    public int IdEmpleado { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Puesto { get; set; } = string.Empty;

    /// <summary>Columna SQL <c>DATE</c>; <see cref="DateOnly"/> evita la parte de hora.</summary>
    public DateOnly FechaContratacion { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
