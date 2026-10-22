namespace Distribuidora.Data.Entities;

/// <summary>
/// Tabla <c>DetallePedidos</c>: tabla intermedia N:M entre Pedidos y Productos
/// con carga útil propia (cantidad y precio unitario), por eso se modela como entidad.
/// </summary>
public class DetallePedido
{
    public int IdDetalle { get; set; }

    public int IdPedido { get; set; }

    public int IdProducto { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public Pedido Pedido { get; set; } = null!;

    public Producto Producto { get; set; } = null!;
}
