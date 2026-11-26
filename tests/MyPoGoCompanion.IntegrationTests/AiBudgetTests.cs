using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class AiBudgetTests(PostgresFixture postgres)
{
    private const string BudgetPath = "/admin/ai/budget";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset _monthStart = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentChatAndResearchAcrossHostsShareOneReservationLedger()
    {
        var connection = await postgres.CreateDatabaseAsync(Ct);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualClock(_monthStart);
        var chatProvider = new FakeProvider(async cancellationToken =>
        {
            started.SetResult();
            await finish.Task.WaitAsync(cancellationToken);
            return new ProviderResult("Chat reply", 2000, 1000, 200, false);
        });
        var researchProvider = new FakeProvider(_ => Task.FromResult(new ProviderResult("Research result", 4000, 0, 1000, true)));
        await using var chatHost = new ApiFactory(connection, settings: Cap("0.11"));
        await using var researchHost = new ApiFactory(connection, settings: Cap("0.11"));
        await using var chatApp = WithProvider(chatHost, chatProvider, clock);
        await using var researchApp = WithProvider(researchHost, researchProvider, clock);
        await using var chatScope = chatApp.Services.CreateAsyncScope();
        await using var researchScope = researchApp.Services.CreateAsyncScope();
        var chatClient = chatScope.ServiceProvider.GetRequiredService<IAiClient>();
        var researchClient = researchScope.ServiceProvider.GetRequiredService<IAiClient>();
        var budget = researchScope.ServiceProvider.GetRequiredService<IAiBudget>();

        var chat = chatClient.GenerateAsync(Request(AiPurpose.Chat), Ct);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        try
        {
            var error = await Assert.ThrowsAsync<AiBudgetExceededException>(
                () => researchClient.GenerateAsync(Request(AiPurpose.Researcher), Ct));
            Assert.Equal(_monthStart.AddMonths(1), error.ResetsAt);
            Assert.Equal(0, researchProvider.Calls);

            var during = await budget.GetStatusAsync(Ct);
            Assert.Equal(0m, during.SpentUsd);
            Assert.Equal(0.104096m, during.ReservedUsd);
            Assert.True(during.SimpleRequestsPaused);
            Assert.True(during.StandardRequestsPaused);
        }
        finally
        {
            finish.SetResult();
        }

        Assert.Equal(0.000675m, (await chat).CostUsd);
        var research = await researchClient.GenerateAsync(Request(AiPurpose.Researcher), Ct);
        Assert.True(research.Incomplete);
        Assert.Equal(0.003m, research.CostUsd);
        var status = await budget.GetStatusAsync(Ct);
        Assert.Equal(0.003675m, status.SpentUsd);
        Assert.Equal(0m, status.ReservedUsd);
        Assert.False(status.StandardRequestsPaused);

        await using var db = PostgresFixture.CreateContext(connection);
        var rows = await db.AiUsage.OrderBy(row => row.Purpose).ToArrayAsync(Ct);
        Assert.Collection(rows,
            row => Assert.Equal((AiPurpose.Chat, 2000, 200, 0.000675m, false),
                (row.Purpose, row.InputTokens, row.OutputTokens, row.CostUsd, row.IsReserved)),
            row => Assert.Equal((AiPurpose.Researcher, 4000, 1000, 0.003m, false),
                (row.Purpose, row.InputTokens, row.OutputTokens, row.CostUsd, row.IsReserved)));
    }

    [Fact]
    public async Task SimultaneousReservationsAllowOnlyOneProviderCallWhenBudgetFitsOne()
    {
        var connection = await postgres.CreateDatabaseAsync(Ct);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeProvider(async cancellationToken =>
        {
            await finish.Task.WaitAsync(cancellationToken);
            return new ProviderResult("Reply", 100, 0, 100, false);
        });
        await using var host = new ApiFactory(connection, settings: Cap("0.11"));
        await using var app = WithProvider(host, provider, new ManualClock(_monthStart));
        await using var chatScope = app.Services.CreateAsyncScope();
        await using var researchScope = app.Services.CreateAsyncScope();
        var chat = chatScope.ServiceProvider.GetRequiredService<IAiClient>().GenerateAsync(Request(AiPurpose.Chat), Ct);
        var research = researchScope.ServiceProvider.GetRequiredService<IAiClient>().GenerateAsync(Request(AiPurpose.Researcher), Ct);

        Task<AiResult> declined;
        try
        {
            declined = await Task.WhenAny(chat, research).WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await Assert.ThrowsAsync<AiBudgetExceededException>(() => declined);
        }
        finally
        {
            finish.TrySetResult();
        }

        var accepted = declined == chat ? research : chat;
        Assert.Equal("Reply", (await accepted).Text);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedCallsReleaseOnlyKnownUnbilledReservationsAndMonthResets(bool definitelyUnbilled)
    {
        var connection = await postgres.CreateDatabaseAsync(Ct);
        var clock = new ManualClock(_monthStart.AddMonths(1).AddSeconds(-1));
        var provider = new FakeProvider(_ => throw new AiProviderException("Provider failure", definitelyUnbilled));
        await using var host = new ApiFactory(connection, settings: Cap("0.11"));
        await using var app = WithProvider(host, provider, clock);
        await using var scope = app.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IAiClient>();
        var budget = scope.ServiceProvider.GetRequiredService<IAiBudget>();
        await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateAsync(Request(AiPurpose.Chat), Ct));
        var before = await budget.GetStatusAsync(Ct);
        Assert.Equal(0m, before.SpentUsd);
        Assert.Equal(definitelyUnbilled ? 0m : 0.104096m, before.ReservedUsd);
        Assert.Equal(!definitelyUnbilled, before.StandardRequestsPaused);
        Assert.Equal(_monthStart.AddMonths(1), before.ResetsAt);

        clock.Now = _monthStart.AddMonths(1);
        var after = await budget.GetStatusAsync(Ct);
        Assert.False(after.StandardRequestsPaused);
        Assert.Equal(0m, after.ReservedUsd);
        Assert.Equal(_monthStart.AddMonths(2), after.ResetsAt);
    }

    [Fact]
    public async Task BudgetStatusIsPrivateAndShowsWhenAiIsDisabled()
    {
        var google = new FakeGoogle().Add("owner", ApiFactory.SuperAdminEmail, "Owner")
            .Add("trainer", "trainer@example.test", "Trainer");
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), google, settings: Cap("0"));
        using var client = api.CreateClient();
        using (var signedOut = await client.GetAsync(BudgetPath, Ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "trainer"));
        using (var trainer = await client.GetAsync(BudgetPath, Ct))
        {
            Assert.Equal(HttpStatusCode.Forbidden, trainer.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "owner"));
        using var owner = await client.GetAsync(BudgetPath, Ct);
        owner.EnsureSuccessStatusCode();
        Assert.True(owner.Headers.CacheControl?.NoStore);
        var status = Assert.IsType<AiBudgetStatus>(await owner.Content.ReadFromJsonAsync<AiBudgetStatus>(Ct));
        Assert.Equal(0m, status.CapUsd);
        Assert.False(status.ProviderConfigured);
        Assert.True(status.SimpleRequestsPaused);
        Assert.True(status.StandardRequestsPaused);
    }

    private static async Task<string> SignInAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync("/auth/google",
            new { code, codeVerifier = new string('v', 64), redirectUri = ApiFactory.WebOrigin + "/auth/callback" }, Ct);
        response.EnsureSuccessStatusCode();
        using var json = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Ct), cancellationToken: Ct);
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static AiRequest Request(AiPurpose purpose) =>
        new(purpose, AiComplexity.Standard, "Give a short answer", "Pokémon GO");

    private static Dictionary<string, string?> Cap(string value) => new() { ["Ai:MonthlyCapUsd"] = value };

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithProvider(
        ApiFactory api, IAiProvider provider, TimeProvider clock) => api.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(provider));
            services.Replace(ServiceDescriptor.Singleton(clock));
        }));

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeProvider(Func<CancellationToken, Task<ProviderResult>> generate) : IAiProvider
    {
        public bool IsConfigured => true;

        public int Calls { get; private set; }

        public Task<ProviderResult> GenerateAsync(AiModel model, AiRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return generate(cancellationToken);
        }
    }
}
