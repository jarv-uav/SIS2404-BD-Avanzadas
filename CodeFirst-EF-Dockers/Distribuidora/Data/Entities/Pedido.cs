namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Pedidos</c>. Relación N:1 con Clientes y N:1 con Empleados.</summary>
public class Pedido
{
    public int IdPedido { get; set; }

    public DateOnly FechaPedido { get; set; }

    public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;

    public int IdCliente { get; set; }

    public int IdEmpleado { get; set; }

    public Cliente Cliente { get; set; } = null!;

    public Empleado Empleado { get; set; } = null!;

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}
