namespace Distribuidora.Infrastructure;

/// <summary>Sección <c>Startup</c> de appsettings: controla qué hace la aplicación al arrancar.</summary>
public sealed class StartupOptions
{
    public const string SectionName = "Startup";

    /// <summary>Aplica las migraciones pendientes (crea la base de datos si no existe).</summary>
    public bool ApplyMigrations { get; set; } = true;

    /// <summary>Ejecuta el <c>DataSeeder</c> idempotente.</summary>
    public bool SeedData { get; set; } = true;

    /// <summary>Ejecuta y muestra las consultas LINQ demostrativas.</summary>
    public bool RunDemoQueries { get; set; } = true;

    /// <summary>Intentos de conexión inicial antes de abortar (útil mientras SQL Server arranca).</summary>
    public int MaxStartupRetries { get; set; } = 5;

    /// <summary>Espera entre intentos de conexión inicial, en segundos.</summary>
    public int RetryDelaySeconds { get; set; } = 3;
}
