namespace MyPoGoCompanion.Api.Configuration;

internal static class CorsSetup
{
    /// <summary>
    /// Lets the web app call the API from its own address. <c>Cors:AllowedOrigins</c> is a comma-separated
    /// list (environment variable <c>Cors__AllowedOrigins</c>), e.g. the Static Web Apps address.
    /// </summary>
    public static IServiceCollection AddWebAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = ParseOrigins(configuration["Cors:AllowedOrigins"]);
        return services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
    }

    public static string[] ParseOrigins(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
