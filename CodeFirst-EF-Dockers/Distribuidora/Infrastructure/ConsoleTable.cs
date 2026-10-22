using System.Globalization;
using System.Text;

namespace Distribuidora.Infrastructure;

public enum Alineacion
{
    Izquierda,
    Derecha
}

/// <summary>
/// Utilidad mínima para imprimir resultados tabulares en consola.
/// Uso: <c>ConsoleTable.De(lista).Col("Id", x =&gt; x.Id, Alineacion.Derecha).Col("Nombre", x =&gt; x.Nombre).Imprimir("Título");</c>
/// </summary>
public static class ConsoleTable
{
    public static TablaBuilder<T> De<T>(IEnumerable<T> filas) => new(filas);

    public static void Seccion(string titulo)
    {
        var linea = new string('═', Math.Max(titulo.Length + 4, 64));
        Console.WriteLine();
        Escribir(linea, ConsoleColor.DarkYellow);
        Escribir($"  {titulo}", ConsoleColor.Yellow);
        Escribir(linea, ConsoleColor.DarkYellow);
    }

    public static void Subtitulo(string texto)
    {
        Console.WriteLine();
        Escribir($"▶ {texto}", ConsoleColor.Cyan);
    }

    public static void Escribir(string texto, ConsoleColor color)
    {
        var anterior = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(texto);
        Console.ForegroundColor = anterior;
    }

    public static string Formatear(object? valor) => valor switch
    {
        null => "(nulo)",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
        _ => valor.ToString() ?? string.Empty
    };
}

public sealed class TablaBuilder<T>
{
    private readonly IReadOnlyList<T> _filas;
    private readonly List<(string Encabezado, Func<T, object?> Valor, Alineacion Alineacion)> _columnas = [];

    internal TablaBuilder(IEnumerable<T> filas) => _filas = filas.ToList();

    public TablaBuilder<T> Col(string encabezado, Func<T, object?> valor, Alineacion alineacion = Alineacion.Izquierda)
    {
        _columnas.Add((encabezado, valor, alineacion));
        return this;
    }

    public void Imprimir(string? subtitulo = null)
    {
        if (subtitulo is not null)
        {
            ConsoleTable.Subtitulo(subtitulo);
        }

        if (_filas.Count == 0)
        {
            Console.WriteLine("  (sin resultados)");
            return;
        }

        var celdas = _filas
            .Select(fila => _columnas.Select(c => ConsoleTable.Formatear(c.Valor(fila))).ToArray())
            .ToList();

        var anchos = _columnas
            .Select((c, i) => Math.Max(c.Encabezado.Length, celdas.Max(f => f[i].Length)))
            .ToArray();

        var separador = "  +" + string.Join("+", anchos.Select(a => new string('-', a + 2))) + "+";

        Console.WriteLine(separador);
        Console.WriteLine(Fila(_columnas.Select(c => c.Encabezado).ToArray(), anchos, encabezado: true));
        Console.WriteLine(separador);
        foreach (var fila in celdas)
        {
            Console.WriteLine(Fila(fila, anchos, encabezado: false));
        }
        Console.WriteLine(separador);
        Console.WriteLine($"  {_filas.Count} registro(s)");
    }

    private string Fila(string[] valores, int[] anchos, bool encabezado)
    {
        var sb = new StringBuilder("  |");
        for (var i = 0; i < valores.Length; i++)
        {
            var alinearDerecha = !encabezado && _columnas[i].Alineacion == Alineacion.Derecha;
            var texto = alinearDerecha ? valores[i].PadLeft(anchos[i]) : valores[i].PadRight(anchos[i]);
            sb.Append(' ').Append(texto).Append(" |");
        }
        return sb.ToString();
    }
}
