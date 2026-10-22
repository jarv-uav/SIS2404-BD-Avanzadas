using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Distribuidora.Data;

/// <summary>
/// Fábrica usada únicamente por las herramientas <c>dotnet ef</c> (migraciones, scripts, update).
/// Lee la misma configuración que la aplicación: appsettings, user-secrets y variables de entorno.
/// </summary>
public sealed class DistribuidoraContextDesignTimeFactory : IDesignTimeDbContextFactory<DistribuidoraContext>
{
    public DistribuidoraContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<DistribuidoraContextDesignTimeFactory>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<DistribuidoraContext>();
        PersistenceSetup.Configure(optionsBuilder, PersistenceSetup.GetConnectionString(configuration));

        return new DistribuidoraContext(optionsBuilder.Options);
    }
}
