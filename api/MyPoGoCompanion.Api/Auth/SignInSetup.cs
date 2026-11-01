using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;

namespace MyPoGoCompanion.Api.Auth;

internal static class SignInSetup
{
    /// <summary>Per client address: 20 sign-in requests a minute (a normal sign-in or refresh needs one or two).</summary>
    public const int RequestsPerMinute = 20;

    /// <summary>
    /// Sign-in, our access tokens (bearer), the role policies, the client-address rate limit on <c>/auth</c>, and the
    /// forwarded-header reading that gives the rate limit the real client address behind Container Apps' ingress.
    /// </summary>
    public static IServiceCollection AddSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        options.GoogleClientId ??= configuration["Auth:Google:ClientId"];
        options.GoogleClientSecret ??= configuration["Auth:Google:ClientSecret"];
        services.AddOptions<AuthOptions>().Configure(o =>
        {
            o.GoogleClientId = options.GoogleClientId;
            o.GoogleClientSecret = options.GoogleClientSecret;
            o.SigningKey = options.SigningKey;
            o.SuperAdminEmail = options.SuperAdminEmail;
        });

        var key = SigningKey.From(options);
        services.AddSingleton(key);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AccessTokens>();
        services.AddScoped<Sessions>();
        services.AddHttpClient<IGoogleTokenExchange, GoogleTokenExchange>(client => client.Timeout = TimeSpan.FromSeconds(15));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(bearer =>
        {
            bearer.MapInboundClaims = false;
            bearer.TokenValidationParameters = AccessTokens.ValidationParameters(key.Value);
        });
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RoleRequirementHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(new RoleRequirement(EffectiveRole.Admin)))
            .AddPolicy(Policies.SuperAdmin, policy => policy.RequireAuthenticatedUser().AddRequirements(new RoleRequirement(EffectiveRole.SuperAdmin)));

        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            // Only Container Apps' ingress can reach the container, so its X-Forwarded-For is trusted (one hop).
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
        });

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };
            limiter.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }

    internal static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address
            ? (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString()
            : IPAddress.None.ToString();
}
