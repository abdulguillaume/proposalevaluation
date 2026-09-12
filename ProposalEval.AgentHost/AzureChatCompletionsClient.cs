using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ProposalEval.AgentHost;

/// <summary>
/// Calls Azure OpenAI chat completions the same way as the working IOM LLM helper:
/// POST {endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-06-01
/// with the api-key header. Avoids the Azure SDK, which can hit the Cognitive Services
/// host that this VNet-restricted resource rejects.
/// </summary>
public sealed class AzureChatCompletionsClient(HttpClient http, string endpoint, string deployment, string apiKey, string apiVersion) : IChatClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ChatClientMetadata _metadata = new("azure-openai", new Uri(endpoint), deployment);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = endpoint.TrimEnd('/');
        var url = $"{baseUrl}/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";

        var payload = new Dictionary<string, object?>
        {
            ["model"] = deployment,
            ["messages"] = messages.Select(ToWireMessage).ToList(),
            ["temperature"] = options?.Temperature ?? 0.2f,
            ["max_tokens"] = options?.MaxOutputTokens ?? 4096
        };

        var tools = ToWireTools(options);
        if (tools.Count > 0)
            payload["tools"] = tools;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM request failed ({(int)response.StatusCode}): {response.ReasonPhrase}. {body}");

        using var doc = JsonDocument.Parse(body);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var contents = ReadContents(message);
        var finish = choice.TryGetProperty("finish_reason", out var reason) && reason.GetString() == "tool_calls"
            ? ChatFinishReason.ToolCalls
            : ChatFinishReason.Stop;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            FinishReason = finish,
            ModelId = deployment
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        var message = response.Messages[0];
        yield return new ChatResponseUpdate(message.Role, message.Contents)
        {
            FinishReason = response.FinishReason,
            ModelId = response.ModelId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey) =>
        serviceKey is null && serviceType == typeof(ChatClientMetadata) ? _metadata : null;

    public void Dispose()
    {
    }

    private static List<object> ToWireTools(ChatOptions? options)
    {
        var tools = new List<object>();
        if (options?.Tools is null)
            return tools;

        foreach (var tool in options.Tools)
        {
            if (tool is not AIFunction function)
                continue;

            tools.Add(new
            {
                type = "function",
                function = new
                {
                    name = function.Name,
                    description = function.Description,
                    parameters = function.JsonSchema
                }
            });
        }

        return tools;
    }

    private static Dictionary<string, object?> ToWireMessage(ChatMessage message)
    {
        var calls = message.Contents.OfType<FunctionCallContent>().ToList();
        var result = message.Contents.OfType<FunctionResultContent>().FirstOrDefault();
        var text = string.Concat(message.Contents.OfType<TextContent>().Select(t => t.Text));

        if (result is not null)
        {
            return new Dictionary<string, object?>
            {
                ["role"] = "tool",
                ["tool_call_id"] = result.CallId,
                ["content"] = result.Result is string s ? s : JsonSerializer.Serialize(result.Result, Json)
            };
        }

        var wire = new Dictionary<string, object?>
        {
            ["role"] = message.Role == ChatRole.System ? "system"
                : message.Role == ChatRole.Assistant ? "assistant"
                : "user",
            ["content"] = string.IsNullOrEmpty(text) ? (calls.Count > 0 ? null : "") : text
        };

        if (calls.Count > 0)
        {
            wire["tool_calls"] = calls.Select(call => new
            {
                id = call.CallId,
                type = "function",
                function = new
                {
                    name = call.Name,
                    arguments = JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>(), Json)
                }
            }).ToList();
        }

        return wire;
    }

    private static List<AIContent> ReadContents(JsonElement message)
    {
        var contents = new List<AIContent>();
        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            if (!string.IsNullOrEmpty(text))
                contents.Add(new TextContent(text));
        }

        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var id = call.GetProperty("id").GetString() ?? "";
                var name = call.GetProperty("function").GetProperty("name").GetString() ?? "";
                var raw = call.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                IDictionary<string, object?> args;
                try
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw, Json)
                           ?? new Dictionary<string, object?>();
                }
                catch (JsonException)
                {
                    args = new Dictionary<string, object?>();
                }

                contents.Add(new FunctionCallContent(id, name, args));
            }
        }

        return contents;
    }
}
