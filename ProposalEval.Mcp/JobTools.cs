using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ProposalEval.Mcp;

[McpServerToolType]
public sealed class JobTools(ProposalEvalApiClient api)
{
    [McpServerTool, Description("Get the evaluation job (type, status, vendor id). Pass the jobId from the application.")]
    public Task<string> get_job(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}", cancellationToken);

    [McpServerTool, Description("Get the vendor profile for this job (name, project id, status). Does not return files.")]
    public Task<string> get_vendor_profile(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/vendor", cancellationToken);

    [McpServerTool, Description("List document metadata for this job: RFQ TOR/strategy and vendor files. Returns id, source (Rfq or Vendor), kind, and file name. Does not return storage keys.")]
    public Task<string> list_vendor_documents(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/documents", cancellationToken);

    [McpServerTool, Description("Read extracted text for one document. source must be Vendor or Rfq. Use an id from list_vendor_documents.")]
    public Task<string> read_document(
        int jobId,
        int documentId,
        [Description("Vendor or Rfq")] string source,
        CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/documents/{documentId}/text?source={Uri.EscapeDataString(source)}", cancellationToken);

    [McpServerTool, Description("List scoring criteria for the RFQ on this job (code, name, weight). Lookup-sized.")]
    public Task<string> list_criteria(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/criteria", cancellationToken);

    [McpServerTool, Description("Save the proposal summary for a Summarize job. Do not include scores. Body is the readable brief.")]
    public Task<string> save_summary(int jobId, string body, CancellationToken cancellationToken) =>
        api.PostJsonAsync($"api/jobs/{jobId}/summary", $$"""{"body":{{System.Text.Json.JsonSerializer.Serialize(body)}}}""", cancellationToken);

    [McpServerTool, Description("Get the latest summary for this job's vendor. Set acceptedOnly true to require an evaluator-accepted summary.")]
    public Task<string> get_summary(int jobId, bool acceptedOnly, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/summary?acceptedOnly={acceptedOnly}", cancellationToken);

    [McpServerTool, Description("Get the accepted summary for this vendor. Scoring must use this, not a draft.")]
    public Task<string> get_accepted_summary(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/summary?acceptedOnly=true", cancellationToken);

    [McpServerTool, Description("Save 0-1 scores for a Score job. itemsJson must be a JSON array, e.g. [{\"rfqCriterionId\":1,\"score\":0.8,\"justification\":\"cite file\"}]. You may use \"code\":\"C1\" instead of rfqCriterionId. Application computes totals.")]
    public Task<string> save_scores(
        int jobId,
        bool mandatoryPass,
        string? recommendation,
        string itemsJson,
        CancellationToken cancellationToken)
    {
        var items = NormalizeItemsJson(itemsJson);
        if (items is null)
            return Task.FromResult("""{"code":400,"error":"itemsJson must be a JSON array of scores."}""");

        var rec = recommendation is null ? "null" : System.Text.Json.JsonSerializer.Serialize(recommendation);
        var json = $$"""{"mandatoryPass":{{mandatoryPass.ToString().ToLowerInvariant()}},"recommendation":{{rec}},"items":{{items}}}""";
        return api.PostJsonAsync($"api/jobs/{jobId}/scores", json, cancellationToken);
    }

    private static string? NormalizeItemsJson(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return null;

        var trimmed = itemsJson.Trim();
        if (trimmed.StartsWith('['))
            return trimmed;

        try
        {
            var inner = System.Text.Json.JsonSerializer.Deserialize<string>(trimmed);
            if (inner is not null && inner.TrimStart().StartsWith('['))
                return inner.Trim();
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return trimmed.StartsWith('{') ? $"[{trimmed}]" : null;
    }

    [McpServerTool, Description("Get saved scores and computed TWS for this job, if any.")]
    public Task<string> get_scores(int jobId, CancellationToken cancellationToken) =>
        api.GetAsync($"api/jobs/{jobId}/scores", cancellationToken);
}
