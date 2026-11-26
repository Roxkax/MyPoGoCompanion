using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyPoGoCompanion.Ai;

public static class AiServices
{
    public static IServiceCollection AddBudgetedAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.Section))
            .Validate(options => options.MonthlyCapUsd >= 0m, "Ai:MonthlyCapUsd must be nonnegative.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IAiProvider, OpenAiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(90);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<AiBudget>();
        services.AddScoped<IAiBudget>(provider => provider.GetRequiredService<AiBudget>());
        services.AddScoped<IAiClient, BudgetedAiClient>();
        return services;
    }
}
