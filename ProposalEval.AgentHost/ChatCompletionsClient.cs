using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ProposalEval.AgentHost;

public enum LlmAuth
{
    Bearer,
    AzureApiKey
}

/// <summary>
/// OpenAI-compatible chat completions. OpenRouter and Groq use Bearer.
/// Azure uses an api-key header on the deployments URL.
/// </summary>
public sealed class ChatCompletionsClient(
    HttpClient http,
    string completionsUrl,
    string model,
    LlmAuth auth,
    string apiKey,
    string? provider = null,
    IReadOnlyDictionary<string, string>? extraHeaders = null) : IChatClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ChatClientMetadata _metadata = new(
        provider ?? (auth == LlmAuth.Bearer ? "openai-compatible" : "azure-openai"),
        new Uri(completionsUrl),
        model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages.SelectMany(ToWireMessages).ToList(),
            ["temperature"] = options?.Temperature ?? 0.2f,
            ["max_tokens"] = options?.MaxOutputTokens ?? 4096
        };

        var tools = ToWireTools(options);
        if (tools.Count > 0)
            payload["tools"] = tools;

        using var request = new HttpRequestMessage(HttpMethod.Post, completionsUrl);
        if (auth == LlmAuth.AzureApiKey)
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        if (extraHeaders is not null)
        {
            foreach (var header in extraHeaders)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

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
            ModelId = model
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

    private static IEnumerable<Dictionary<string, object?>> ToWireMessages(ChatMessage message)
    {
        var calls = message.Contents.OfType<FunctionCallContent>().ToList();
        var results = message.Contents.OfType<FunctionResultContent>().ToList();
        var text = string.Concat(message.Contents.OfType<TextContent>().Select(t => t.Text));

        if (calls.Count > 0 || (results.Count == 0 && message.Role != ChatRole.Tool))
        {
            var role = message.Role == ChatRole.System ? "system"
                : message.Role == ChatRole.Assistant || calls.Count > 0 ? "assistant"
                : "user";

            var wire = new Dictionary<string, object?>
            {
                ["role"] = role,
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
                        arguments = SerializeArguments(call.Arguments)
                    }
                }).ToList();
            }

            yield return wire;
        }

        foreach (var result in results)
        {
            yield return new Dictionary<string, object?>
            {
                ["role"] = "tool",
                ["tool_call_id"] = result.CallId,
                ["content"] = result.Result is string s ? s : JsonSerializer.Serialize(result.Result, Json)
            };
        }
    }

    private static string SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return "{}";

        return JsonSerializer.Serialize(arguments, Json);
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
