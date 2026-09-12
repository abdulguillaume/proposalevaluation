using Microsoft.EntityFrameworkCore;
using ProposalEval.Api;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class JobAgentService(AppDbContext db, IFileStore files)
{
    public async Task<OpResult<JobDto>> GetJobAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await db.EvaluationJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        return job is null
            ? OpResult<JobDto>.Fail(404, "Job not found.")
            : OpResult<JobDto>.Success(MapJob(job));
    }

    public async Task<OpResult<VendorProfileDto>> GetVendorAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<VendorProfileDto>.Fail(404, "Job not found.");
        if (job.Vendor is null)
            return OpResult<VendorProfileDto>.Fail(400, "This job is not tied to a vendor.");

        var v = job.Vendor;
        return OpResult<VendorProfileDto>.Success(
            new VendorProfileDto(v.Id, v.RfqId, v.Name, v.ExternalProjectId, v.Status.ToString()));
    }

    public async Task<OpResult<IReadOnlyList<DocumentDto>>> ListDocumentsAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<IReadOnlyList<DocumentDto>>.Fail(404, "Job not found.");

        var rfqDocs = await db.RfqDocuments.AsNoTracking()
            .Where(d => d.RfqId == job.RfqId)
            .Select(d => new DocumentDto(d.Id, "Rfq", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
            .ToListAsync(cancellationToken);

        var vendorDocs = job.VendorId is null
            ? []
            : await db.VendorDocuments.AsNoTracking()
                .Where(d => d.VendorId == job.VendorId)
                .Select(d => new DocumentDto(d.Id, "Vendor", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
                .ToListAsync(cancellationToken);

        return OpResult<IReadOnlyList<DocumentDto>>.Success(rfqDocs.Concat(vendorDocs).ToList());
    }

    public async Task<OpResult<DocumentTextDto>> ReadDocumentAsync(int jobId, int documentId, string source, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<DocumentTextDto>.Fail(404, "Job not found.");

        string storageKey;
        string kind;
        string fileName;
        string normalized = source.Equals("Rfq", StringComparison.OrdinalIgnoreCase) ? "Rfq" : "Vendor";

        if (normalized == "Rfq")
        {
            var doc = await db.RfqDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.RfqId == job.RfqId, cancellationToken);
            if (doc is null)
                return OpResult<DocumentTextDto>.Fail(404, "Document not found for this job.");
            storageKey = doc.StorageKey;
            kind = doc.Kind.ToString();
            fileName = doc.FileName;
        }
        else
        {
            if (job.VendorId is null)
                return OpResult<DocumentTextDto>.Fail(400, "This job is not tied to a vendor.");
            var doc = await db.VendorDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.VendorId == job.VendorId, cancellationToken);
            if (doc is null)
                return OpResult<DocumentTextDto>.Fail(404, "Document not found for this job.");
            storageKey = doc.StorageKey;
            kind = doc.Kind.ToString();
            fileName = doc.FileName;
        }

        var stored = await files.GetAsync(BlobContainers.ProposalEvals, storageKey, cancellationToken);
        if (stored is null)
            return OpResult<DocumentTextDto>.Fail(404, "File is not in storage.");

        var text = DocumentTextExtractor.Extract(stored);
        return OpResult<DocumentTextDto>.Success(new DocumentTextDto(documentId, normalized, kind, fileName, text));
    }

    public async Task<OpResult<IReadOnlyList<CriterionDto>>> ListCriteriaAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<IReadOnlyList<CriterionDto>>.Fail(404, "Job not found.");

        var items = await db.RfqCriteria.AsNoTracking()
            .Where(c => c.RfqId == job.RfqId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CriterionDto(c.Id, c.Code, c.Name, c.Weight, c.GroupCode, c.GroupWeight, c.SortOrder))
            .ToListAsync(cancellationToken);
        return OpResult<IReadOnlyList<CriterionDto>>.Success(items);
    }

    public async Task<OpResult<SummaryDto>> SaveSummaryAsync(int jobId, SaveSummaryRequest request, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<SummaryDto>.Fail(404, "Job not found.");
        if (job.JobType != JobType.Summarize)
            return OpResult<SummaryDto>.Fail(400, "This job is not a summary job.");
        if (job.VendorId is null)
            return OpResult<SummaryDto>.Fail(400, "This job is not tied to a vendor.");
        if (string.IsNullOrWhiteSpace(request.Body))
            return OpResult<SummaryDto>.Fail(400, "Summary body is required.");

        var summary = new ProposalSummary
        {
            JobId = job.Id,
            VendorId = job.VendorId.Value,
            Body = request.Body.Trim(),
            Status = SummaryStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.ProposalSummaries.Add(summary);
        job.Status = JobStatus.WaitingReview;
        job.StatusMessage = "Summary saved. Waiting for review.";
        job.Vendor!.Status = VendorStatus.AwaitingSummaryReview;
        job.Vendor.UpdatedAt = DateTime.UtcNow;
        db.EvaluationJobEvents.Add(new EvaluationJobEvent
        {
            JobId = job.Id,
            Status = JobStatus.WaitingReview,
            Message = "Summary written.",
            At = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<SummaryDto>.Success(MapSummary(summary), 201);
    }

    public async Task<OpResult<SummaryDto>> GetSummaryAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<SummaryDto>.Fail(404, "Job not found.");
        if (job.VendorId is null)
            return OpResult<SummaryDto>.Fail(400, "This job is not tied to a vendor.");

        var summary = await db.ProposalSummaries.AsNoTracking()
            .Where(s => s.VendorId == job.VendorId)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return summary is null
            ? OpResult<SummaryDto>.Fail(404, "No summary for this vendor.")
            : OpResult<SummaryDto>.Success(MapSummary(summary));
    }

    public async Task<OpResult<SummaryDto>> GetAcceptedSummaryAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<SummaryDto>.Fail(404, "Job not found.");
        if (job.VendorId is null)
            return OpResult<SummaryDto>.Fail(400, "This job is not tied to a vendor.");

        var summary = await db.ProposalSummaries.AsNoTracking()
            .Where(s => s.VendorId == job.VendorId && s.Status == SummaryStatus.Accepted)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return summary is null
            ? OpResult<SummaryDto>.Fail(404, "No accepted summary for this vendor.")
            : OpResult<SummaryDto>.Success(MapSummary(summary));
    }

    public async Task<OpResult<VendorEvaluationDto>> SaveScoresAsync(int jobId, SaveScoresRequest request, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<VendorEvaluationDto>.Fail(404, "Job not found.");
        if (job.JobType != JobType.Score)
            return OpResult<VendorEvaluationDto>.Fail(400, "This job is not a scoring job.");
        if (job.VendorId is null)
            return OpResult<VendorEvaluationDto>.Fail(404, "This job is not tied to a vendor.");
        if (request.Items is null || request.Items.Count == 0)
            return OpResult<VendorEvaluationDto>.Fail(400, "At least one score item is required.");

        var accepted = await db.ProposalSummaries.AnyAsync(
            s => s.VendorId == job.VendorId && s.Status == SummaryStatus.Accepted, cancellationToken);
        if (!accepted)
            return OpResult<VendorEvaluationDto>.Fail(409, "Summary must be accepted before scores can be saved.");

        var criteria = await db.RfqCriteria.Where(c => c.RfqId == job.RfqId).ToListAsync(cancellationToken);
        if (criteria.Count == 0)
            return OpResult<VendorEvaluationDto>.Fail(400, "No criteria are defined for this RFQ.");

        foreach (var item in request.Items)
        {
            if (item.Score is < 0 or > 1)
                return OpResult<VendorEvaluationDto>.Fail(400, "Each score must be between 0.00 and 1.00.");
            if (criteria.All(c => c.Id != item.RfqCriterionId))
                return OpResult<VendorEvaluationDto>.Fail(400, $"Criterion {item.RfqCriterionId} does not belong to this RFQ.");
        }

        var existing = await db.VendorEvaluations
            .Include(e => e.Scores)
            .FirstOrDefaultAsync(e => e.JobId == job.Id && e.VendorId == job.VendorId, cancellationToken);
        if (existing is not null)
        {
            db.VendorScores.RemoveRange(existing.Scores);
            db.VendorEvaluations.Remove(existing);
        }

        var evaluation = new VendorEvaluation
        {
            JobId = job.Id,
            VendorId = job.VendorId.Value,
            MandatoryPass = request.MandatoryPass,
            Recommendation = request.Recommendation,
            Status = ScoreStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        foreach (var item in request.Items)
        {
            evaluation.Scores.Add(new VendorScore
            {
                RfqCriterionId = item.RfqCriterionId,
                Score = decimal.Round(item.Score, 2),
                Justification = item.Justification
            });
        }

        db.VendorEvaluations.Add(evaluation);
        job.Status = JobStatus.WaitingReview;
        job.StatusMessage = "Scores saved. Waiting for review.";
        job.Vendor!.Status = VendorStatus.AwaitingScoreReview;
        job.Vendor.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var saved = await GetEvaluationAsync(job.Id, cancellationToken);
        return saved.Ok
            ? OpResult<VendorEvaluationDto>.Success(saved.Data!, 201)
            : saved;
    }

    public async Task<OpResult<VendorEvaluationDto>> GetEvaluationAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(jobId, cancellationToken);
        if (job is null)
            return OpResult<VendorEvaluationDto>.Fail(404, "Job not found.");

        var evaluation = await db.VendorEvaluations
            .Include(e => e.Scores)
            .ThenInclude(s => s.Criterion)
            .FirstOrDefaultAsync(e => e.JobId == jobId, cancellationToken);

        return evaluation is null
            ? OpResult<VendorEvaluationDto>.Fail(404, "No scores for this job.")
            : OpResult<VendorEvaluationDto>.Success(MapEvaluation(evaluation));
    }

    private async Task<EvaluationJob?> LoadJobAsync(int jobId, CancellationToken cancellationToken) =>
        await db.EvaluationJobs
            .Include(j => j.Vendor)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    private static JobDto MapJob(EvaluationJob job) =>
        new(job.Id, job.RunId, job.RfqId, job.VendorId, job.JobType.ToString(), job.Status.ToString(), job.StatusMessage, job.CreatedAt);

    private static SummaryDto MapSummary(ProposalSummary s) =>
        new(s.Id, s.JobId, s.VendorId, s.Body, s.Status.ToString(), s.RejectionReason, s.CreatedAt);

    private static VendorEvaluationDto MapEvaluation(VendorEvaluation e) =>
        new(
            e.Id,
            e.JobId,
            e.VendorId,
            e.MandatoryPass,
            e.Recommendation,
            e.Status.ToString(),
            e.Tws,
            e.TechnicalPass,
            e.Scores.Select(s => new VendorScoreDto(
                s.Id,
                s.RfqCriterionId,
                s.Criterion.Code,
                s.Criterion.Name,
                s.Score,
                s.Justification)).ToList());
}
