using Microsoft.EntityFrameworkCore;
using ProposalEval.Api;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class EvaluationService(AppDbContext db, AgentJobQueue agentJobs)
{
    public const string AgentStartedMessage = "The agent has started working.";

    public async Task<(bool Ok, string Message)> StartAsync(int rfqId, CancellationToken cancellationToken = default)
    {
        var rfq = await db.Rfqs
            .Include(r => r.Documents)
            .Include(r => r.Vendors)
            .Include(r => r.Runs)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);

        if (rfq is null)
            return (false, "RFQ not found.");

        var latest = rfq.Runs.OrderByDescending(r => r.Id).FirstOrDefault();
        if (latest is not null)
            return await EnqueuePendingVendorsAsync(rfq, latest, cancellationToken);

        if (rfq.Documents.All(d => d.Kind != RfqDocumentKind.Tor))
            return (false, "Load the TOR before requesting evaluation.");

        if (rfq.Documents.All(d => d.Kind != RfqDocumentKind.ScoringStrategy))
            return (false, "Load the scoring strategy before requesting evaluation.");

        if (rfq.Vendors.Count == 0)
            return (false, "Add at least one vendor profile before requesting evaluation.");

        await DefaultRfqCriteria.EnsureAsync(db, rfq.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var run = new EvaluationRun
        {
            RfqId = rfq.Id,
            Status = RunStatus.Running,
            StatusMessage = AgentStartedMessage,
            RequestedBy = "staff",
            CreatedAt = now,
            StartedAt = now
        };

        foreach (var vendor in rfq.Vendors)
        {
            vendor.Status = VendorStatus.Summarizing;
            vendor.UpdatedAt = now;
            run.Jobs.Add(CreateJob(rfq.Id, vendor.Id, JobType.Summarize, now));
        }

        rfq.Status = RfqStatus.Evaluating;
        rfq.UpdatedAt = now;
        db.EvaluationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var job in run.Jobs)
            agentJobs.Enqueue(job.Id);

        return (true, run.StatusMessage);
    }

    private async Task<(bool Ok, string Message)> EnqueuePendingVendorsAsync(
        Rfq rfq,
        EvaluationRun run,
        CancellationToken cancellationToken)
    {
        var startedIds = run.Jobs
            .Where(j => j.JobType == JobType.Summarize && j.VendorId is not null)
            .Select(j => j.VendorId!.Value)
            .ToHashSet();

        var pending = rfq.Vendors.Where(v => !startedIds.Contains(v.Id)).OrderBy(v => v.Id).ToList();
        if (pending.Count == 0)
            return (false, "Every vendor already has a summary job. Retry a failed job to start a new attempt.");

        await DefaultRfqCriteria.EnsureAsync(db, rfq.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var created = new List<EvaluationJob>();
        foreach (var vendor in pending)
        {
            vendor.Status = VendorStatus.Summarizing;
            vendor.UpdatedAt = now;
            var job = CreateJob(rfq.Id, vendor.Id, JobType.Summarize, now);
            run.Jobs.Add(job);
            created.Add(job);
        }

        ApplyRunStatus(run);
        rfq.Status = RfqStatus.Evaluating;
        rfq.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var job in created)
            agentJobs.Enqueue(job.Id);

        return (true, created.Count == 1
            ? AgentStartedMessage
            : $"{created.Count} new summary jobs queued.");
    }

    public async Task<(bool Ok, string Message)> RetryFailedAsync(int rfqId, int? jobId, CancellationToken cancellationToken = default)
    {
        var rfq = await db.Rfqs
            .Include(r => r.Vendors)
            .Include(r => r.Runs)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);

        if (rfq is null)
            return (false, "RFQ not found.");

        var run = rfq.Runs.OrderByDescending(r => r.Id).FirstOrDefault();
        if (run is null)
            return (false, "Start evaluation before retrying a job.");

        List<EvaluationJob> failed;
        if (jobId is int id)
        {
            var job = run.Jobs.FirstOrDefault(j => j.Id == id);
            if (job is null)
                return (false, "Job not found on this RFQ.");
            if (!IsRetryable(run, job))
                return (false, "Only a failed job that is the latest attempt can be retried. The failed job is kept.");

            failed = [job];
        }
        else
        {
            failed = run.Jobs.Where(j => IsRetryable(run, j)).OrderBy(j => j.Id).ToList();
            if (failed.Count == 0)
                return (false, "There is no failed job to retry.");
        }

        var now = DateTime.UtcNow;
        var created = new List<EvaluationJob>();
        foreach (var failedJob in failed)
        {
            if (failedJob.Status == JobStatus.Running)
            {
                failedJob.Status = JobStatus.Failed;
                failedJob.StatusMessage = "Replaced by a retry.";
                failedJob.CompletedAt = now;
            }

            var next = CreateJob(rfq.Id, failedJob.VendorId, failedJob.JobType, now);
            run.Jobs.Add(next);
            created.Add(next);

            if (failedJob.VendorId is int vendorId)
            {
                var vendor = rfq.Vendors.FirstOrDefault(v => v.Id == vendorId);
                if (vendor is not null)
                {
                    vendor.Status = failedJob.JobType == JobType.Score ? VendorStatus.Scoring : VendorStatus.Summarizing;
                    vendor.UpdatedAt = now;
                }
            }
        }

        ApplyRunStatus(run);
        rfq.Status = RfqStatus.Evaluating;
        rfq.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var job in created)
            agentJobs.Enqueue(job.Id);

        return (true, created.Count == 1
            ? AgentStartedMessage
            : $"{created.Count} new jobs queued. Failed jobs are kept.");
    }

    public async Task<OpResult<SummaryDto>> ReviewSummaryAsync(int jobId, bool accept, string? reason, CancellationToken cancellationToken = default)
    {
        var job = await db.EvaluationJobs
            .Include(j => j.Vendor)
            .Include(j => j.Run)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
            return OpResult<SummaryDto>.Fail(404, "Job not found.");
        if (job.JobType != JobType.Summarize)
            return OpResult<SummaryDto>.Fail(400, "This job is not a summary job.");
        if (job.Vendor is null)
            return OpResult<SummaryDto>.Fail(400, "This job is not tied to a vendor.");

        var summary = await db.ProposalSummaries
            .Where(s => s.JobId == job.Id)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null)
            return OpResult<SummaryDto>.Fail(404, "No summary for this job.");
        if (summary.Status != SummaryStatus.Draft)
            return OpResult<SummaryDto>.Fail(409, "This summary has already been reviewed.");

        var now = DateTime.UtcNow;
        if (accept)
        {
            summary.Status = SummaryStatus.Accepted;
            summary.RejectionReason = null;
            summary.ReviewedAt = now;
            job.Status = JobStatus.Completed;
            job.StatusMessage = "Summary accepted.";
            job.CompletedAt = now;
            job.Vendor.Status = VendorStatus.Scoring;
            job.Vendor.UpdatedAt = now;
            db.EvaluationJobEvents.Add(new EvaluationJobEvent
            {
                JobId = job.Id,
                Status = JobStatus.Completed,
                Message = "Summary accepted.",
                At = now
            });

            var scoreJob = CreateJob(job.RfqId, job.VendorId, JobType.Score, now);
            job.Run.Jobs.Add(scoreJob);
            ApplyRunStatus(job.Run);
            await db.SaveChangesAsync(cancellationToken);
            agentJobs.Enqueue(scoreJob.Id);
            return OpResult<SummaryDto>.Success(MapSummary(summary));
        }

        var trimmed = reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            return OpResult<SummaryDto>.Fail(400, "A rejection reason is required.");
        if (trimmed.Length > 2000)
            return OpResult<SummaryDto>.Fail(400, "Rejection reason must be 2000 characters or fewer.");
        if (LooksLikePathProbe(trimmed))
            return OpResult<SummaryDto>.Fail(400, "Rejection reason is not valid.");

        summary.Status = SummaryStatus.Rejected;
        summary.RejectionReason = trimmed;
        summary.ReviewedAt = now;
        job.Status = JobStatus.Completed;
        job.StatusMessage = "Summary rejected.";
        job.CompletedAt = now;
        job.Vendor.Status = VendorStatus.SummaryRejected;
        job.Vendor.UpdatedAt = now;
        db.EvaluationJobEvents.Add(new EvaluationJobEvent
        {
            JobId = job.Id,
            Status = JobStatus.Completed,
            Message = "Summary rejected.",
            At = now
        });
        ApplyRunStatus(job.Run);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<SummaryDto>.Success(MapSummary(summary));
    }

    public async Task<OpResult<VendorEvaluationDto>> ReviewScoresAsync(int jobId, bool accept, string? reason, CancellationToken cancellationToken = default)
    {
        var job = await db.EvaluationJobs
            .Include(j => j.Vendor)
            .Include(j => j.Run)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
            return OpResult<VendorEvaluationDto>.Fail(404, "Job not found.");
        if (job.JobType != JobType.Score)
            return OpResult<VendorEvaluationDto>.Fail(400, "This job is not a scoring job.");
        if (job.Vendor is null)
            return OpResult<VendorEvaluationDto>.Fail(400, "This job is not tied to a vendor.");

        var evaluation = await db.VendorEvaluations
            .Include(e => e.Scores)
            .ThenInclude(s => s.Criterion)
            .FirstOrDefaultAsync(e => e.JobId == job.Id, cancellationToken);

        if (evaluation is null)
            return OpResult<VendorEvaluationDto>.Fail(404, "No scores for this job.");
        if (evaluation.Status != ScoreStatus.Draft)
            return OpResult<VendorEvaluationDto>.Fail(409, "These scores have already been reviewed.");

        var now = DateTime.UtcNow;
        if (accept)
        {
            evaluation.Status = ScoreStatus.Approved;
            evaluation.ApprovedAt = now;
            job.Status = JobStatus.Completed;
            job.StatusMessage = "Scores accepted.";
            job.CompletedAt = now;
            job.Vendor.Status = VendorStatus.Approved;
            job.Vendor.UpdatedAt = now;
            db.EvaluationJobEvents.Add(new EvaluationJobEvent
            {
                JobId = job.Id,
                Status = JobStatus.Completed,
                Message = "Scores accepted.",
                At = now
            });
            ApplyRunStatus(job.Run);
            await db.SaveChangesAsync(cancellationToken);
            return OpResult<VendorEvaluationDto>.Success(MapEvaluation(evaluation));
        }

        var trimmed = reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            return OpResult<VendorEvaluationDto>.Fail(400, "A rejection reason is required.");
        if (trimmed.Length > 2000)
            return OpResult<VendorEvaluationDto>.Fail(400, "Rejection reason must be 2000 characters or fewer.");
        if (LooksLikePathProbe(trimmed))
            return OpResult<VendorEvaluationDto>.Fail(400, "Rejection reason is not valid.");

        evaluation.Status = ScoreStatus.Rejected;
        evaluation.ApprovedAt = null;
        job.Status = JobStatus.Completed;
        job.StatusMessage = trimmed.Length <= 1000 ? $"Scores rejected. {trimmed}" : $"Scores rejected. {trimmed[..997]}...";
        job.CompletedAt = now;
        job.Vendor.Status = VendorStatus.ScoreRejected;
        job.Vendor.UpdatedAt = now;
        db.EvaluationJobEvents.Add(new EvaluationJobEvent
        {
            JobId = job.Id,
            Status = JobStatus.Completed,
            Message = "Scores rejected.",
            At = now
        });
        ApplyRunStatus(job.Run);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<VendorEvaluationDto>.Success(MapEvaluation(evaluation));
    }

    public static bool IsRetryable(EvaluationRun run, EvaluationJob job)
    {
        if (job.Status is not (JobStatus.Failed or JobStatus.Running))
            return false;

        var latestId = run.Jobs
            .Where(j => j.VendorId == job.VendorId && j.JobType == job.JobType)
            .Max(j => j.Id);
        return job.Id == latestId;
    }

    public static void ApplyRunStatus(EvaluationRun run)
    {
        var latest = run.Jobs
            .GroupBy(j => (j.VendorId, j.JobType))
            .Select(g => g.MaxBy(j => j.Id)!)
            .ToList();

        if (latest.Count == 0)
            return;

        if (latest.Any(j => j.Status is JobStatus.Queued or JobStatus.Running))
        {
            var scoreInFlight = latest.Any(j => j.JobType == JobType.Score && j.Status is JobStatus.Queued or JobStatus.Running);
            var summaryInFlight = latest.Any(j => j.JobType == JobType.Summarize && j.Status is JobStatus.Queued or JobStatus.Running);
            run.Status = scoreInFlight && !summaryInFlight ? RunStatus.Scoring : RunStatus.Running;
            run.StatusMessage = AgentStartedMessage;
            run.CompletedAt = null;
            return;
        }

        var failed = latest.Count(j => j.Status == JobStatus.Failed);
        var waiting = latest.Count(j => j.Status == JobStatus.WaitingReview);

        if (waiting > 0)
        {
            run.Status = latest.Any(j => j.JobType == JobType.Score)
                ? RunStatus.WaitingScoreReview
                : RunStatus.WaitingSummaryReview;
            run.StatusMessage = failed > 0
                ? $"{waiting} job(s) waiting for review. {failed} failed — retry to start a new job."
                : "Waiting for review.";
            run.CompletedAt = null;
            return;
        }

        if (failed > 0)
        {
            run.Status = RunStatus.Failed;
            run.StatusMessage = failed == latest.Count
                ? "Evaluation failed. Retry to start a new job; failed jobs are kept."
                : $"{failed} job(s) failed. Retry to start a new job; failed jobs are kept.";
            run.CompletedAt = DateTime.UtcNow;
            return;
        }

        if (latest.All(j => j.Status is JobStatus.Completed or JobStatus.Cancelled))
        {
            run.Status = RunStatus.Completed;
            run.StatusMessage = "Evaluation completed.";
            run.CompletedAt = DateTime.UtcNow;
        }
    }

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

    private static bool LooksLikePathProbe(string value) =>
        value.Contains("://", StringComparison.Ordinal)
        || value.Contains("..", StringComparison.Ordinal)
        || value.StartsWith('/')
        || value.StartsWith('\\')
        || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'));

    private static EvaluationJob CreateJob(int rfqId, int? vendorId, JobType jobType, DateTime now) =>
        new()
        {
            RfqId = rfqId,
            VendorId = vendorId,
            JobType = jobType,
            Status = JobStatus.Queued,
            StatusMessage = AgentStartedMessage,
            CreatedAt = now,
            Events =
            {
                new EvaluationJobEvent
                {
                    Status = JobStatus.Queued,
                    Message = jobType == JobType.Score
                        ? "Job created. Waiting for the scoring agent."
                        : "Job created. Waiting for the summary agent.",
                    At = now
                }
            }
        };
}
