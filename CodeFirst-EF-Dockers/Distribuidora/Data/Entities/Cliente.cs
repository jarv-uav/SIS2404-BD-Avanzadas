namespace Distribuidora.Data.Entities;

/// <summary>Tabla <c>Clientes</c>. Un cliente realiza N pedidos.</summary>
public class Cliente
{
    public int IdCliente { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
