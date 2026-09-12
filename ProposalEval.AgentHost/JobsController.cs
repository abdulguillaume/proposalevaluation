using Microsoft.AspNetCore.Mvc;

namespace ProposalEval.AgentHost;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ILogger<JobsController> logger, AgentRunService runs) : ApiControllerBase
{
    private readonly ILogger<JobsController> _logger = logger;

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(int id, [FromQuery] string? agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting agent run for job {JobId} agent={Agent}.", id, agent);
        try
        {
            var (status, data, error) = await runs.RunAsync(id, agent, cancellationToken);
            if (data is null)
                return Fail(status, error ?? "Agent run failed.");

            _logger.LogInformation("Finished agent run for job {JobId} ({Agent}).", data.JobId, data.Agent);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run agent for job {JobId}.", id);
            return Fail(500, "Failed to run agent.");
        }
    }
}
