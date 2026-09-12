using System.Text.Json;

namespace ProposalEval.AgentHost;

public sealed class AppApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static Uri ResolveBaseAddress()
    {
        var raw = Environment.GetEnvironmentVariable("PROPOSAL_EVAL_API_BASE");
        if (string.IsNullOrWhiteSpace(raw))
            raw = "http://localhost:5018";
        if (!raw.EndsWith('/'))
            raw += "/";
        return new Uri(raw);
    }

    public async Task<(int Status, PreparedPromptDto? Prompt, string? Error)> GetPromptAsync(
        int jobId,
        string? agent,
        CancellationToken cancellationToken)
    {
        var path = $"api/jobs/{jobId}/prompt";
        if (!string.IsNullOrWhiteSpace(agent))
            path += $"?agent={Uri.EscapeDataString(agent)}";

        using var response = await http.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Envelope<PreparedPromptDto>? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope<PreparedPromptDto>>(body, Json);
        }
        catch (JsonException)
        {
            return ((int)response.StatusCode, null, string.IsNullOrWhiteSpace(body) ? "The application returned an unreadable prompt response." : body);
        }

        if (envelope is null)
            return ((int)response.StatusCode, null, "The application returned an empty prompt response.");

        if (!response.IsSuccessStatusCode)
            return (envelope.Code != 0 ? envelope.Code : (int)response.StatusCode, null, envelope.Error ?? "Failed to load prepared prompt.");

        if (envelope.Data is null || string.IsNullOrWhiteSpace(envelope.Data.Prompt))
            return (500, null, "Prepared prompt was empty.");

        return (200, envelope.Data, null);
    }
}

public sealed class Envelope<T>
{
    public int Code { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public sealed class PreparedPromptDto
{
    public int JobId { get; set; }
    public string Agent { get; set; } = "";
    public string JobType { get; set; } = "";
    public string Prompt { get; set; } = "";
}
