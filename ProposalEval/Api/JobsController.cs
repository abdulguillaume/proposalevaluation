using Microsoft.AspNetCore.Mvc;
using ProposalEval.Services;

namespace ProposalEval.Api;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ILogger<JobsController> logger, JobAgentService jobs, PreparedPromptService prompts) : ApiControllerBase
{
    private readonly ILogger<JobsController> _logger = logger;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting job {JobId}.", id);
        try
        {
            var result = await jobs.GetJobAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded job {JobId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}.", id);
            return Fail(500, "Failed to get job.");
        }
    }

    [HttpGet("{id:int}/prompt")]
    public async Task<IActionResult> GetPrompt(int id, [FromQuery] string? agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting prepared prompt for job {JobId} agent={Agent}.", id, agent);
        try
        {
            var result = await prompts.GetAsync(id, agent, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded {Agent} prompt for job {JobId}.", result.Data!.Agent, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prepared prompt for job {JobId}.", id);
            return Fail(500, "Failed to get prepared prompt.");
        }
    }

    [HttpGet("{id:int}/vendor")]
    public async Task<IActionResult> GetVendor(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting vendor profile for job {JobId}.", id);
        try
        {
            var result = await jobs.GetVendorAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded vendor for job {JobId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor for job {JobId}.", id);
            return Fail(500, "Failed to get vendor for job.");
        }
    }

    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> ListDocuments(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing documents for job {JobId}.", id);
        try
        {
            var result = await jobs.ListDocumentsAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Listed {Count} documents for job {JobId}.", result.Data!.Count, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents for job {JobId}.", id);
            return Fail(500, "Failed to list job documents.");
        }
    }

    [HttpGet("{id:int}/documents/{documentId:int}/text")]
    public async Task<IActionResult> ReadDocument(int id, int documentId, [FromQuery] string source = "Vendor", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reading {Source} document {DocumentId} for job {JobId}.", source, documentId, id);
        try
        {
            var result = await jobs.ReadDocumentAsync(id, documentId, source, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Read document {DocumentId} for job {JobId}.", documentId, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read document {DocumentId} for job {JobId}.", documentId, id);
            return Fail(500, "Failed to read document.");
        }
    }

    [HttpGet("{id:int}/criteria")]
    public async Task<IActionResult> ListCriteria(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing criteria for job {JobId}.", id);
        try
        {
            var result = await jobs.ListCriteriaAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Listed {Count} criteria for job {JobId}.", result.Data!.Count, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list criteria for job {JobId}.", id);
            return Fail(500, "Failed to list job criteria.");
        }
    }

    [HttpPost("{id:int}/summary")]
    public async Task<IActionResult> SaveSummary(int id, [FromBody] SaveSummaryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving summary for job {JobId}.", id);
        try
        {
            var result = await jobs.SaveSummaryAsync(id, request, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Saved summary {SummaryId} for job {JobId}.", result.Data!.Id, id);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save summary for job {JobId}.", id);
            return Fail(500, "Failed to save summary.");
        }
    }

    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> GetSummary(int id, [FromQuery] bool acceptedOnly = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting summary for job {JobId} acceptedOnly={AcceptedOnly}.", id, acceptedOnly);
        try
        {
            var result = acceptedOnly
                ? await jobs.GetAcceptedSummaryAsync(id, cancellationToken)
                : await jobs.GetSummaryAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded summary for job {JobId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get summary for job {JobId}.", id);
            return Fail(500, "Failed to get summary.");
        }
    }

    [HttpPost("{id:int}/scores")]
    public async Task<IActionResult> SaveScores(int id, [FromBody] SaveScoresRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving scores for job {JobId}.", id);
        try
        {
            var result = await jobs.SaveScoresAsync(id, request, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Saved scores for job {JobId} TWS {Tws}.", id, result.Data!.Tws);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scores for job {JobId}.", id);
            return Fail(500, "Failed to save scores.");
        }
    }

    [HttpGet("{id:int}/scores")]
    public async Task<IActionResult> GetScores(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting scores for job {JobId}.", id);
        try
        {
            var result = await jobs.GetEvaluationAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded scores for job {JobId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scores for job {JobId}.", id);
            return Fail(500, "Failed to get scores.");
        }
    }
}
