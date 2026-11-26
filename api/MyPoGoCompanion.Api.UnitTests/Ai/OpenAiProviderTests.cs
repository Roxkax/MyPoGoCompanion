using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.UnitTests.Ai;

public sealed class OpenAiProviderTests
{
    private const string Mini = "gpt-5-mini-2025-08-07";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AiComplexity.Simple, "gpt-5-nano-2025-08-07", "0.000135", "0.020820")]
    [InlineData(AiComplexity.Standard, Mini, "0.000675", "0.104096")]
    public void RoutesByTaskAndPricesCachedInputSeparately(AiComplexity complexity, string name, string cost, string reserve)
    {
        var model = AiModel.For(complexity);
        Assert.Equal(name, model.Name);
        Assert.Equal(decimal.Parse(cost, System.Globalization.CultureInfo.InvariantCulture), model.Cost(2_000, 1_000, 200));
        Assert.Equal(decimal.Parse(reserve, System.Globalization.CultureInfo.InvariantCulture), model.Reserve(2048));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiModel.For((AiComplexity)42));
    }

    [Theory]
    [InlineData("completed", false)]
    [InlineData("incomplete", true)]
    public async Task SendsPrivateBoundedRequestAndReadsBillableUsage(string status, bool incomplete)
    {
        using var http = Client(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(Mini, body.RootElement.GetProperty("model").GetString());
            Assert.Equal("Instructions", body.RootElement.GetProperty("instructions").GetString());
            Assert.Equal("Question", body.RootElement.GetProperty("input").GetString());
            Assert.Equal(128, body.RootElement.GetProperty("max_output_tokens").GetInt32());
            Assert.False(body.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal("default", body.RootElement.GetProperty("service_tier").GetString());

            return Response(HttpStatusCode.OK, $$"""
                {
                  "status":"{{status}}", "model":"{{Mini}}",
                  "usage":{"input_tokens":100,"input_tokens_details":{"cached_tokens":32},"output_tokens":48},
                  "output":[{"type":"reasoning"},{"type":"message","content":[
                     {"type":"output_text","text":"First"},{"type":"output_text","text":"Second"}]}]
                }
                """);
        });

        var provider = Provider(http);
        Assert.True(provider.IsConfigured);
        var result = await provider.GenerateAsync(AiModel.For(AiComplexity.Standard), Request(), Ct);
        Assert.Equal(new ProviderResult("First\nSecond", 100, 32, 48, incomplete), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    [InlineData(HttpStatusCode.Redirect, false)]
    public async Task ReportsProviderErrorsWithoutExposingTheBody(HttpStatusCode status, bool definitelyUnbilled)
    {
        using var http = Client((_, _) => Task.FromResult(Response(status, "private data")));
        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(http).GenerateAsync(AiModel.For(AiComplexity.Standard), Request(), Ct));
        Assert.Equal(definitelyUnbilled, exception.DefinitelyUnbilled);
        Assert.DoesNotContain("private data", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("malformed")]
    [InlineData("""{"status":"completed","model":"unexpected","usage":{"input_tokens":100,"output_tokens":10},"output":[]}""")]
    [InlineData("""{"status":"completed","model":"gpt-5-mini-2025-08-07","usage":{"input_tokens":100,"output_tokens":129},"output":[]}""")]
    public async Task TreatsUnknownUsageAsPotentiallyBilled(string body)
    {
        using var http = Client((_, _) => Task.FromResult(Response(HttpStatusCode.OK, body)));
        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(http).GenerateAsync(AiModel.For(AiComplexity.Standard), Request(), Ct));
        Assert.False(exception.DefinitelyUnbilled);
    }

    [Fact]
    public async Task UnconfiguredProviderAndInvalidRequestsNeverReachBudgetOrTransport()
    {
        using var http = Client((_, _) => throw new InvalidOperationException("Unexpected provider call."));
        var provider = new OpenAiProvider(http, Options.Create(new AiOptions()));
        Assert.False(provider.IsConfigured);
        var client = new BudgetedAiClient(provider, null!);

        await Assert.ThrowsAsync<AiUnavailableException>(() => client.GenerateAsync(Request(), Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GenerateAsync(Request() with { MaxOutputTokens = 0 }, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GenerateAsync(Request() with { Purpose = (AiPurpose)42 }, Ct));
    }

    private static AiRequest Request() => new(AiPurpose.Chat, AiComplexity.Standard, "Instructions", "Question", 128);

    private static OpenAiProvider Provider(HttpClient http) =>
        new(http, Options.Create(new AiOptions { OpenAiApiKey = "test-key" }));

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send)) { BaseAddress = new Uri("https://api.openai.com/v1/") };

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
