using Microsoft.AspNetCore.Mvc;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Api;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqsController(ILogger<RfqsController> logger, RfqAppService rfqs, VendorAppService vendors) : ApiControllerBase
{
    private readonly ILogger<RfqsController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> List(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing RFQs page {PageNumber} size {PageSize}.", pageNumber, pageSize);
        try
        {
            var data = await rfqs.ListAsync(pageNumber, pageSize, cancellationToken);
            _logger.LogInformation("Listed {Count} of {Total} RFQs.", data.Items.Count, data.TotalCount);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list RFQs.");
            return Fail(500, "Failed to list RFQs.");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.GetAsync(id, cancellationToken);
            if (!result.Ok)
            {
                _logger.LogInformation("RFQ {RfqId} not found.", id);
                return Fail(result.Status, result.Error!);
            }

            _logger.LogInformation("Loaded RFQ {RfqId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get RFQ {RfqId}.", id);
            return Fail(500, "Failed to get RFQ.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRfqRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating RFQ {Number}.", request.Number);
        try
        {
            var result = await rfqs.CreateAsync(request, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Created RFQ {RfqId}.", result.Data!.Id);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RFQ.");
            return Fail(500, "Failed to create RFQ.");
        }
    }

    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> ListDocuments(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing documents for RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.ListDocumentsAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Listed {Count} RFQ documents.", result.Data!.Count);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list RFQ {RfqId} documents.", id);
            return Fail(500, "Failed to list RFQ documents.");
        }
    }

    [HttpPost("{id:int}/documents")]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] RfqDocumentKind kind, IFormFile? file, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading {Kind} for RFQ {RfqId}.", kind, id);
        try
        {
            if (file is null)
                return Fail(400, "Choose a file to upload.");

            var result = await rfqs.UploadDocumentAsync(id, kind, file, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Uploaded document {DocumentId} for RFQ {RfqId}.", result.Data!.Id, id);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document for RFQ {RfqId}.", id);
            return Fail(500, "Failed to upload RFQ document.");
        }
    }

    [HttpGet("{id:int}/criteria")]
    public async Task<IActionResult> ListCriteria(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing criteria for RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.ListCriteriaAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Listed {Count} criteria for RFQ {RfqId}.", result.Data!.Count, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list criteria for RFQ {RfqId}.", id);
            return Fail(500, "Failed to list criteria.");
        }
    }

    [HttpPost("{id:int}/criteria")]
    public async Task<IActionResult> UpsertCriteria(int id, [FromBody] UpsertCriteriaRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving criteria for RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.UpsertCriteriaAsync(id, request, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Saved {Count} criteria for RFQ {RfqId}.", result.Data!.Count, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save criteria for RFQ {RfqId}.", id);
            return Fail(500, "Failed to save criteria.");
        }
    }

    [HttpPost("{id:int}/evaluate")]
    public async Task<IActionResult> Evaluate(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting evaluation for RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.EvaluateAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Evaluation run {RunId} for RFQ {RfqId}.", result.Data!.RunId, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start evaluation for RFQ {RfqId}.", id);
            return Fail(500, "Failed to start evaluation.");
        }
    }

    [HttpPost("{id:int}/retry-failed")]
    public async Task<IActionResult> RetryFailed(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrying failed jobs for RFQ {RfqId}.", id);
        try
        {
            var result = await rfqs.RetryFailedAsync(id, null, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Retried failed jobs on run {RunId} for RFQ {RfqId}.", result.Data!.RunId, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry jobs for RFQ {RfqId}.", id);
            return Fail(500, "Failed to retry jobs.");
        }
    }

    [HttpPost("{id:int}/jobs/{jobId:int}/retry")]
    public async Task<IActionResult> RetryJob(int id, int jobId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrying job {JobId} on RFQ {RfqId}.", jobId, id);
        try
        {
            var result = await rfqs.RetryFailedAsync(id, jobId, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Queued a new job after failed job {JobId} on RFQ {RfqId}.", jobId, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry job {JobId} on RFQ {RfqId}.", jobId, id);
            return Fail(500, "Failed to retry job.");
        }
    }

    [HttpGet("{id:int}/vendors")]
    public async Task<IActionResult> ListVendors(int id, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing vendors for RFQ {RfqId}.", id);
        try
        {
            var data = await rfqs.ListVendorsAsync(id, pageNumber, pageSize, cancellationToken);
            if (data is null)
                return Fail(404, "RFQ not found.");

            _logger.LogInformation("Listed {Count} of {Total} vendors for RFQ {RfqId}.", data.Items.Count, data.TotalCount, id);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list vendors for RFQ {RfqId}.", id);
            return Fail(500, "Failed to list vendors.");
        }
    }

    [HttpPost("{id:int}/vendors")]
    public async Task<IActionResult> CreateVendor(int id, [FromBody] CreateVendorRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating vendor on RFQ {RfqId}.", id);
        try
        {
            var result = await vendors.CreateAsync(id, request, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Created vendor {VendorId} on RFQ {RfqId}.", result.Data!.Id, id);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create vendor on RFQ {RfqId}.", id);
            return Fail(500, "Failed to create vendor.");
        }
    }

    [HttpGet("{id:int}/jobs")]
    public async Task<IActionResult> ListJobs(int id, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing jobs for RFQ {RfqId}.", id);
        try
        {
            var data = await rfqs.ListJobsAsync(id, pageNumber, pageSize, cancellationToken);
            if (data is null)
                return Fail(404, "RFQ not found.");

            _logger.LogInformation("Listed {Count} of {Total} jobs for RFQ {RfqId}.", data.Items.Count, data.TotalCount, id);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list jobs for RFQ {RfqId}.", id);
            return Fail(500, "Failed to list jobs.");
        }
    }
}
