using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Api;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class PreparedPromptService(AppDbContext db, IHostEnvironment env)
{
    public async Task<OpResult<PreparedPromptDto>> GetAsync(int jobId, string? agent, CancellationToken cancellationToken)
    {
        var job = await db.EvaluationJobs
            .Include(j => j.Vendor)
            .Include(j => j.Rfq)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
            return OpResult<PreparedPromptDto>.Fail(404, "Job not found.");
        if (job.Vendor is null)
            return OpResult<PreparedPromptDto>.Fail(400, "This job is not tied to a vendor.");

        var kind = ResolveAgent(agent, job.JobType);
        if (kind is null)
            return OpResult<PreparedPromptDto>.Fail(400, "Agent must be summary or score.");

        var vendorDocs = await db.VendorDocuments.AsNoTracking()
            .Where(d => d.VendorId == job.VendorId)
            .OrderBy(d => d.Kind)
            .ToListAsync(cancellationToken);

        var rfqDocs = await db.RfqDocuments.AsNoTracking()
            .Where(d => d.RfqId == job.RfqId)
            .OrderBy(d => d.Kind)
            .ToListAsync(cancellationToken);

        var tor = rfqDocs.FirstOrDefault(d => d.Kind == RfqDocumentKind.Tor);
        var strategy = rfqDocs.FirstOrDefault(d => d.Kind == RfqDocumentKind.ScoringStrategy);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["job_id"] = job.Id.ToString(),
            ["vendor_name"] = job.Vendor.Name,
            ["project_id"] = job.Vendor.ExternalProjectId ?? "—",
            ["rfq_number"] = job.Rfq.Number,
            ["rfq_title"] = job.Rfq.Title,
            ["document_inventory"] = FormatDocs(vendorDocs.Select(d => (d.Id, "Vendor", d.Kind.ToString(), d.FileName))),
            ["tor_file"] = FormatRfqDoc(tor, "TOR"),
            ["scoring_strategy_file"] = FormatRfqDoc(strategy, "Scoring strategy"),
            ["accepted_summary"] = "",
            ["criteria_list"] = ""
        };

        if (kind == "score")
        {
            var accepted = await db.ProposalSummaries.AsNoTracking()
                .Where(s => s.VendorId == job.VendorId && s.Status == SummaryStatus.Accepted)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (accepted is null)
                return OpResult<PreparedPromptDto>.Fail(409, "No accepted summary for this vendor. Scoring prompt is not available.");

            await DefaultRfqCriteria.EnsureAsync(db, job.RfqId, cancellationToken);
            var criteria = await db.RfqCriteria.AsNoTracking()
                .Where(c => c.RfqId == job.RfqId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);

            values["accepted_summary"] = accepted.Body;
            values["criteria_list"] = FormatCriteria(criteria);
        }

        var templatePath = TemplatePath(kind);
        if (templatePath is null)
            return OpResult<PreparedPromptDto>.Fail(500, $"Prepared prompt template for {kind} is missing.");

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
        var prompt = Fill(template, values);
        return OpResult<PreparedPromptDto>.Success(new PreparedPromptDto(job.Id, kind, job.JobType.ToString(), prompt));
    }

    private static string? ResolveAgent(string? agent, JobType jobType)
    {
        if (!string.IsNullOrWhiteSpace(agent))
        {
            if (agent.Equals("summary", StringComparison.OrdinalIgnoreCase))
                return "summary";
            if (agent.Equals("score", StringComparison.OrdinalIgnoreCase))
                return "score";
            return null;
        }

        return jobType == JobType.Score ? "score" : "summary";
    }

    private string? TemplatePath(string kind)
    {
        var file = kind == "score" ? "score.txt" : "summary.txt";
        var fromContent = Path.Combine(env.ContentRootPath, "Prompts", file);
        if (File.Exists(fromContent))
            return fromContent;
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "Prompts", file);
        return File.Exists(fromOutput) ? fromOutput : null;
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Regex.Replace(template, @"\{\{([a-z_]+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value : match.Value;
        }, RegexOptions.IgnoreCase);

    private static string FormatDocs(IEnumerable<(int Id, string Source, string Kind, string FileName)> docs)
    {
        var list = docs.ToList();
        if (list.Count == 0)
            return "(none)";

        var sb = new StringBuilder();
        foreach (var d in list)
            sb.AppendLine($"- id={d.Id} source={d.Source} kind={d.Kind} fileName={d.FileName}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatRfqDoc(RfqDocument? doc, string label) =>
        doc is null
            ? $"({label} not loaded)"
            : $"id={doc.Id} source=Rfq kind={doc.Kind} fileName={doc.FileName}";

    private static string FormatCriteria(IReadOnlyList<RfqCriterion> criteria)
    {
        if (criteria.Count == 0)
            return "(no criteria defined for this RFQ)";

        var sb = new StringBuilder();
        foreach (var c in criteria)
        {
            var weight = c.Weight is null ? "—" : c.Weight.Value.ToString("0.##");
            var group = string.IsNullOrEmpty(c.GroupCode) ? "" : $" group={c.GroupCode}";
            sb.AppendLine($"- id={c.Id} code={c.Code} name={c.Name} weight={weight}{group}");
        }

        return sb.ToString().TrimEnd();
    }
}
