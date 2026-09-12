using Microsoft.AspNetCore.Mvc;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Api;

[ApiController]
[Route("api/vendors")]
public sealed class VendorsController(ILogger<VendorsController> logger, VendorAppService vendors) : ApiControllerBase
{
    private readonly ILogger<VendorsController> _logger = logger;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting vendor {VendorId}.", id);
        try
        {
            var result = await vendors.GetAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Loaded vendor {VendorId}.", id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor {VendorId}.", id);
            return Fail(500, "Failed to get vendor.");
        }
    }

    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> ListDocuments(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing documents for vendor {VendorId}.", id);
        try
        {
            var result = await vendors.ListDocumentsAsync(id, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Listed {Count} documents for vendor {VendorId}.", result.Data!.Count, id);
            return Success(200, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents for vendor {VendorId}.", id);
            return Fail(500, "Failed to list vendor documents.");
        }
    }

    [HttpPost("{id:int}/documents")]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] VendorDocumentKind kind, IFormFile? file, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading {Kind} for vendor {VendorId}.", kind, id);
        try
        {
            if (file is null)
                return Fail(400, "Choose a file to upload.");

            var result = await vendors.UploadDocumentAsync(id, kind, file, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Uploaded document {DocumentId} for vendor {VendorId}.", result.Data!.Id, id);
            return Success(201, result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document for vendor {VendorId}.", id);
            return Fail(500, "Failed to upload vendor document.");
        }
    }

    [HttpDelete("{id:int}/documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting document {DocumentId} for vendor {VendorId}.", documentId, id);
        try
        {
            var result = await vendors.DeleteDocumentAsync(id, documentId, cancellationToken);
            if (!result.Ok)
                return Fail(result.Status, result.Error!);

            _logger.LogInformation("Deleted document {DocumentId} for vendor {VendorId}.", documentId, id);
            return Success(200, new { deleted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId} for vendor {VendorId}.", documentId, id);
            return Fail(500, "Failed to delete vendor document.");
        }
    }
}
