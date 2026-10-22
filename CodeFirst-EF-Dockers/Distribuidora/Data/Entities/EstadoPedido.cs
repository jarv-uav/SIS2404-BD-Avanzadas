namespace Distribuidora.Data.Entities;

/// <summary>
/// Valores permitidos por el CHECK de <c>Pedidos.estado</c>.
/// Se persisten como texto (VARCHAR(20)) mediante una conversión de valor en Fluent API,
/// así la base de datos conserva exactamente los mismos literales que el script SQL.
/// </summary>
public enum EstadoPedido
{
    Pendiente = 0,
    Enviado = 1,
    Entregado = 2,
    Cancelado = 3
}
