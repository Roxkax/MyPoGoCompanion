using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using Npgsql;

namespace MyPoGoCompanion.Api.Configuration;

internal static class DatabaseSetup
{
    /// <summary>
    /// Registers the PostgreSQL data source and <see cref="AppDbContext"/> from <c>ConnectionStrings:Database</c>
    /// (environment variable <c>ConnectionStrings__Database</c>). Without one, the API still starts (and the
    /// OpenAPI document can be written at build time): <c>/health</c> reports the database as unhealthy and endpoints
    /// that need it fail.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddNpgsqlDataSource(DatabaseConnectionString.Normalize(connectionString));
            services.AddDbContext<AppDbContext>((provider, options) => options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql());
        }

        return services;
    }
}
