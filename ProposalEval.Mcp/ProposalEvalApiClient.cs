using System.Net.Http.Headers;
using System.Text;

namespace ProposalEval.Mcp;

public sealed class ProposalEvalApiClient(HttpClient http)
{
    public Task<string> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, path, null, cancellationToken);

    public Task<string> PostJsonAsync(string path, string json, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, json, cancellationToken);

    private async Task<string> SendAsync(HttpMethod method, string path, string? json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public static class ApiBaseAddress
{
    public static Uri Resolve()
    {
        var raw = Environment.GetEnvironmentVariable("PROPOSAL_EVAL_API_BASE");
        if (string.IsNullOrWhiteSpace(raw))
            raw = "http://localhost:5018";
        if (!raw.EndsWith('/'))
            raw += "/";
        return new Uri(raw);
    }
}
