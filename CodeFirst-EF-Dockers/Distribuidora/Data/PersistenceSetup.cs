using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Distribuidora.Data;

/// <summary>
/// Registro de la capa de persistencia. Se comparte entre el arranque de la aplicación
/// y la fábrica de diseño (<c>dotnet ef</c>) para que ambos usen exactamente la misma configuración.
/// </summary>
public static class PersistenceSetup
{
    public const string ConnectionStringName = "Distribuidora";

    public static IServiceCollection AddDistribuidoraPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<DistribuidoraContext>(options => Configure(options, connectionString));
        services.AddScoped<DataSeeder>();

        return services;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No se encontró la cadena de conexión '{ConnectionStringName}'. " +
                $"Defínela en appsettings.json, en user-secrets o con la variable de entorno " +
                $"ConnectionStrings__{ConnectionStringName}.");
        }

        return connectionString;
    }

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            // Reintentos ante errores transitorios (red, failover, servidor arrancando).
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sql.CommandTimeout(60);
        });

        // La migración inicial está escrita a mano para replicar el script (incluye ON UPDATE CASCADE).
        // Si el snapshot difiere en algún detalle cosmético del modelo, EF lo registra como advertencia
        // en lugar de abortar el arranque.
        options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
    }
}
