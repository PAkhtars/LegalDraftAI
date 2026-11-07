using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LegalDraftAI.Models;
using Microsoft.Extensions.Options;

namespace LegalDraftAI.Services;

public sealed class GeminiDraftService : IGeminiDraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;

    public GeminiDraftService(HttpClient http, IOptions<GeminiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<DraftResult> GenerateStructuredDraftAsync(
        DraftRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new DraftResult
            {
                Success = false,
                Error = "Gemini API key is not configured. Set Gemini:ApiKey using .NET User Secrets or the Gemini__ApiKey environment variable."
            };
        }

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = GroqDraftService.BuildSystemPrompt(
                            request.DocumentType,
                            request.Jurisdiction,
                            request.OutputLanguage)
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = GroqDraftService.BuildUserPrompt(request) } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2,
                maxOutputTokens = 8192
            }
        };

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"models/{Uri.EscapeDataString(_options.Model)}:generateContent");
        requestMessage.Headers.Add("x-goog-api-key", _options.ApiKey);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(requestMessage, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new DraftResult
            {
                Success = false,
                Error = $"Gemini API error {(int)response.StatusCode}: {responseText}"
            };
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0 ||
                candidates[0].ValueKind != JsonValueKind.Object ||
                !candidates[0].TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Object ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                return new DraftResult
                {
                    Success = false,
                    Error = "Gemini returned no draft content."
                };
            }

            var messageContent = string.Concat(
                parts.EnumerateArray()
                    .Where(part => part.TryGetProperty("text", out _))
                    .Select(part => part.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(messageContent))
            {
                return new DraftResult
                {
                    Success = false,
                    Error = "Gemini returned empty draft content."
                };
            }

            using var structured = JsonDocument.Parse(messageContent);
            var usage = root.TryGetProperty("usageMetadata", out var usageMetadata)
                ? usageMetadata
                : default;

            return new DraftResult
            {
                Success = true,
                RawJson = messageContent,
                StructuredData = JsonSerializer.Deserialize<object>(messageContent),
                PromptTokens = GetTokenCount(usage, "promptTokenCount"),
                CompletionTokens = GetTokenCount(usage, "candidatesTokenCount")
            };
        }
        catch (JsonException exception)
        {
            return new DraftResult
            {
                Success = false,
                Error = $"Gemini returned invalid JSON: {exception.Message}"
            };
        }
    }

    private static int GetTokenCount(JsonElement usageMetadata, string propertyName) =>
        usageMetadata.ValueKind == JsonValueKind.Object &&
        usageMetadata.TryGetProperty(propertyName, out var tokenCount) &&
        tokenCount.TryGetInt32(out var count)
            ? count
            : 0;
}
