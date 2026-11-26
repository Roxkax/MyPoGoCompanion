using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MyPoGoCompanion.Ai;

internal interface IAiProvider
{
    bool IsConfigured { get; }

    Task<ProviderResult> GenerateAsync(AiModel model, AiRequest request, CancellationToken cancellationToken);
}

internal sealed record ProviderResult(string Text, int InputTokens, int CachedInputTokens, int OutputTokens, bool Incomplete);

internal sealed class OpenAiProvider(HttpClient http, IOptions<AiOptions> options) : IAiProvider
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.OpenAiApiKey);

    public async Task<ProviderResult> GenerateAsync(AiModel model, AiRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.OpenAiApiKey?.Trim());
        message.Content = JsonContent.Create(new
        {
            model = model.Name,
            instructions = request.Instructions,
            input = request.Input,
            max_output_tokens = request.MaxOutputTokens,
            store = false,
            service_tier = "default",
        });
        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var rejected = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.RequestEntityTooLarge
                or HttpStatusCode.UnprocessableContent or HttpStatusCode.TooManyRequests;
            throw new AiProviderException($"OpenAI returned HTTP {(int)response.StatusCode}.", rejected);
        }

        try
        {
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
            return Parse(json?.RootElement ?? throw new JsonException(), model, request.MaxOutputTokens);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or
            InvalidOperationException or FormatException)
        {
            throw new AiProviderException("OpenAI returned an invalid response or usage report.", innerException: exception);
        }
    }

    private static ProviderResult Parse(JsonElement root, AiModel model, int outputLimit)
    {
        var status = root.GetProperty("status").GetString();
        var usage = root.GetProperty("usage");
        var input = usage.GetProperty("input_tokens").GetInt32();
        var output = usage.GetProperty("output_tokens").GetInt32();
        var cached = usage.TryGetProperty("input_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object
            && details.TryGetProperty("cached_tokens", out var cachedTokens) ? cachedTokens.GetInt32() : 0;
        if (status is not ("completed" or "incomplete") || root.GetProperty("model").GetString() != model.Name
            || input < 0 || input > model.ContextTokens || cached < 0 || cached > input || output < 0 || output > outputLimit)
        {
            throw new JsonException();
        }

        var text = new List<string>();
        foreach (var item in root.GetProperty("output").EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "message")
            {
                continue;
            }

            foreach (var part in item.GetProperty("content").EnumerateArray())
            {
                if (part.GetProperty("type").GetString() == "output_text")
                {
                    text.Add(part.GetProperty("text").GetString() ?? throw new JsonException());
                }
            }
        }

        return new ProviderResult(string.Join('\n', text), input, cached, output, status == "incomplete");
    }
}
