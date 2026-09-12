using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class AgentDispatchWorker(
    AgentJobQueue queue,
    IHttpClientFactory httpFactory,
    IServiceScopeFactory scopes,
    ILogger<AgentDispatchWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DispatchAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch job {JobId} to the agent host.", jobId);
                await MarkFailedAsync(jobId, "The agent host could not complete this job.", stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(int jobId, CancellationToken cancellationToken)
    {
        string? agent;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await db.EvaluationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                logger.LogWarning("Job {JobId} was queued for the agent host but was not found.", jobId);
                return;
            }

            agent = job.JobType == JobType.Score ? "score" : "summary";
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.StatusMessage = EvaluationService.AgentStartedMessage;
            db.EvaluationJobEvents.Add(new EvaluationJobEvent
            {
                JobId = job.Id,
                Status = JobStatus.Running,
                Message = "Dispatched to the agent host.",
                At = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Calling agent host for job {JobId} ({Agent}).", jobId, agent);
        var client = httpFactory.CreateClient("AgentHost");
        using var response = await client.PostAsync($"api/jobs/{jobId}/run?agent={agent}", null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Agent host finished job {JobId}.", jobId);
            await RefreshRunAsync(jobId, cancellationToken);
            return;
        }

        var error = ReadError(body) ?? $"Agent host returned {(int)response.StatusCode}.";
        logger.LogWarning("Agent host failed job {JobId}: {Error}", jobId, error);
        await MarkFailedAsync(jobId, Truncate(error), cancellationToken);
    }

    private async Task MarkFailedAsync(int jobId, string message, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.EvaluationJobs
            .Include(j => j.Run)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return;

        job.Status = JobStatus.Failed;
        job.StatusMessage = Truncate(message);
        job.CompletedAt = DateTime.UtcNow;
        db.EvaluationJobEvents.Add(new EvaluationJobEvent
        {
            JobId = job.Id,
            Status = JobStatus.Failed,
            Message = job.StatusMessage,
            At = DateTime.UtcNow
        });
        EvaluationService.ApplyRunStatus(job.Run);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RefreshRunAsync(int jobId, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.EvaluationJobs
            .Include(j => j.Run)
            .ThenInclude(r => r.Jobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return;

        EvaluationService.ApplyRunStatus(job.Run);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? ReadError(string body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<HostEnvelope>(body, Json);
            return string.IsNullOrWhiteSpace(envelope?.Error) ? null : envelope.Error;
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
    }

    private static string Truncate(string message) =>
        message.Length <= 1000 ? message : message[..997] + "...";

    private sealed class HostEnvelope
    {
        public int Code { get; set; }
        public string? Error { get; set; }
    }
}
