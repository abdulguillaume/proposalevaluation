using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Pages.Rfqs;

public class DetailsModel(AppDbContext db, IFileStore files, EvaluationService evaluation) : PageModel
{
    public Rfq Rfq { get; private set; } = null!;
    public EvaluationRun? LatestRun { get; private set; }
    public string? Flash { get; private set; }
    public string? Error { get; private set; }

    public bool HasTor => Rfq.Documents.Any(d => d.Kind == RfqDocumentKind.Tor);
    public bool HasStrategy => Rfq.Documents.Any(d => d.Kind == RfqDocumentKind.ScoringStrategy);
    public bool CanEvaluate => HasTor && HasStrategy && Rfq.Vendors.Count > 0 && LatestRun is null;
    public int PendingVendorCount => LatestRun is null
        ? 0
        : Rfq.Vendors.Count(v => LatestRun.Jobs.All(j => j.VendorId != v.Id || j.JobType != JobType.Summarize));
    public bool CanEvaluatePending => HasTor && HasStrategy && PendingVendorCount > 0;
    public bool CanRetryFailed => LatestRun is not null && LatestRun.Jobs.Any(j => EvaluationService.IsRetryable(LatestRun, j));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, RfqDocumentKind kind, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id))
            return NotFound();

        if (file is null || file.Length == 0)
        {
            Error = "Choose a file to upload.";
            return Page();
        }

        var existing = Rfq.Documents.FirstOrDefault(d => d.Kind == kind);
        if (existing is not null)
        {
            await files.DeleteAsync(BlobContainers.ProposalEvals, existing.StorageKey);
            db.RfqDocuments.Remove(existing);
        }

        var key = await files.SaveAsync(BlobContainers.ProposalEvals, BlobFolders.Rfq(Rfq.Id), file.FileName, file.ContentType, file.OpenReadStream());
        db.RfqDocuments.Add(new RfqDocument
        {
            RfqId = Rfq.Id,
            Kind = kind,
            FileName = BlobNaming.OriginalName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            UploadedAt = DateTime.UtcNow
        });

        RefreshRfqReady();
        await db.SaveChangesAsync();
        if (kind == RfqDocumentKind.ScoringStrategy)
            await DefaultRfqCriteria.EnsureAsync(db, Rfq.Id, cancellationToken);
        Flash = $"{StatusLabels.RfqDocument(kind)} loaded.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEvaluateAsync(int id)
    {
        var (ok, message) = await evaluation.StartAsync(id);
        if (!ok)
        {
            if (!await LoadAsync(id))
                return NotFound();
            Error = message;
            return Page();
        }

        TempData["Flash"] = message;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRetryAsync(int id, int? jobId)
    {
        var (ok, message) = await evaluation.RetryFailedAsync(id, jobId);
        if (!ok)
        {
            if (!await LoadAsync(id))
                return NotFound();
            Error = message;
            return Page();
        }

        TempData["Flash"] = message;
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var rfq = await db.Rfqs
            .Include(r => r.Documents)
            .Include(r => r.Vendors)
            .ThenInclude(v => v.Documents)
            .Include(r => r.Runs)
            .ThenInclude(r => r.Jobs)
            .ThenInclude(j => j.Vendor)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rfq is null)
            return false;

        Rfq = rfq;
        LatestRun = rfq.Runs.OrderByDescending(r => r.Id).FirstOrDefault();
        Flash = TempData["Flash"] as string;
        return true;
    }

    private void RefreshRfqReady()
    {
        if (Rfq.Status is RfqStatus.Evaluating or RfqStatus.Completed)
            return;

        var ready = Rfq.Documents.Any(d => d.Kind == RfqDocumentKind.Tor)
                    && Rfq.Documents.Any(d => d.Kind == RfqDocumentKind.ScoringStrategy)
                    && Rfq.Vendors.Count > 0;
        Rfq.Status = ready ? RfqStatus.Ready : RfqStatus.Draft;
        Rfq.UpdatedAt = DateTime.UtcNow;
    }
}
