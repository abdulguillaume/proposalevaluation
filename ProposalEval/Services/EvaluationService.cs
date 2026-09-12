using Microsoft.EntityFrameworkCore;
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
        {
            if (latest.Status is RunStatus.Requested or RunStatus.Running)
                return (true, latest.StatusMessage);

            return (false, "Evaluation already started. Retry a failed job to start a new attempt.");
        }

        if (rfq.Documents.All(d => d.Kind != RfqDocumentKind.Tor))
            return (false, "Load the TOR before requesting evaluation.");

        if (rfq.Documents.All(d => d.Kind != RfqDocumentKind.ScoringStrategy))
            return (false, "Load the scoring strategy before requesting evaluation.");

        if (rfq.Vendors.Count == 0)
            return (false, "Add at least one vendor profile before requesting evaluation.");

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

    public static bool IsRetryable(EvaluationRun run, EvaluationJob job)
    {
        if (job.Status != JobStatus.Failed)
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
            run.Status = RunStatus.Running;
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
