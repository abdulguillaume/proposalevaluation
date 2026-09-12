using Microsoft.AspNetCore.Mvc;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Api;

[ApiController]
[Route("api/lookups")]
public sealed class LookupsController(ILogger<LookupsController> logger) : ApiControllerBase
{
    private readonly ILogger<LookupsController> _logger = logger;

    [HttpGet("rfq-document-kinds")]
    public async Task<IActionResult> RfqDocumentKinds()
    {
        _logger.LogInformation("Listing RFQ document kind lookup.");
        try
        {
            await Task.CompletedTask;
            var data = Enum.GetValues<RfqDocumentKind>()
                .Select(v => new LookupItemDto(v.ToString(), StatusLabels.RfqDocument(v)))
                .ToList();
            _logger.LogInformation("Returned {Count} RFQ document kinds.", data.Count);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list RFQ document kinds.");
            return Fail(500, "Failed to list RFQ document kinds.");
        }
    }

    [HttpGet("vendor-document-kinds")]
    public async Task<IActionResult> VendorDocumentKinds()
    {
        _logger.LogInformation("Listing vendor document kind lookup.");
        try
        {
            await Task.CompletedTask;
            var data = Enum.GetValues<VendorDocumentKind>()
                .Select(v => new LookupItemDto(v.ToString(), StatusLabels.VendorDocument(v)))
                .ToList();
            _logger.LogInformation("Returned {Count} vendor document kinds.", data.Count);
            return Success(200, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list vendor document kinds.");
            return Fail(500, "Failed to list vendor document kinds.");
        }
    }
}
