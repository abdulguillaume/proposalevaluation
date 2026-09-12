using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Pages.Vendors;

public class DetailsModel(AppDbContext db, IFileStore files) : PageModel
{
    public Vendor Vendor { get; private set; } = null!;
    public int? ReviewJobId { get; private set; }
    public int? ScoresJobId { get; private set; }
    public string? Flash { get; private set; }
    public string? Error { get; private set; }

    public IReadOnlyList<VendorDocumentKind> RequiredKinds { get; } =
    [
        VendorDocumentKind.Technical,
        VendorDocumentKind.Financial,
        VendorDocumentKind.Cvs,
        VendorDocumentKind.Portfolio,
        VendorDocumentKind.Permit
    ];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, VendorDocumentKind kind, IFormFile? file)
    {
        if (!await LoadAsync(id))
            return NotFound();

        if (file is null || file.Length == 0)
        {
            Error = "Choose a file to upload.";
            return Page();
        }

        var key = await files.SaveAsync(BlobContainers.ProposalEvals, BlobFolders.Vendor(Vendor.Name, Vendor.ExternalProjectId), file.FileName, file.ContentType, file.OpenReadStream());
        db.VendorDocuments.Add(new VendorDocument
        {
            VendorId = Vendor.Id,
            Kind = kind,
            FileName = BlobNaming.OriginalName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            UploadedAt = DateTime.UtcNow
        });

        if (Vendor.Status == VendorStatus.Draft)
            Vendor.Status = VendorStatus.Ready;
        Vendor.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["Flash"] = $"{StatusLabels.VendorDocument(kind)} loaded.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int documentId)
    {
        if (!await LoadAsync(id))
            return NotFound();

        var document = Vendor.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
            return NotFound();

        await files.DeleteAsync(BlobContainers.ProposalEvals, document.StorageKey);
        db.VendorDocuments.Remove(document);
        if (Vendor.Status == VendorStatus.Ready && Vendor.Documents.Count <= 1)
            Vendor.Status = VendorStatus.Draft;
        Vendor.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var vendor = await db.Vendors
            .Include(v => v.Rfq)
            .Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vendor is null)
            return false;

        Vendor = vendor;
        ReviewJobId = await db.ProposalSummaries
            .Where(s => s.VendorId == vendor.Id)
            .OrderByDescending(s => s.Id)
            .Select(s => (int?)s.JobId)
            .FirstOrDefaultAsync();
        ScoresJobId = await db.VendorEvaluations
            .Where(e => e.VendorId == vendor.Id)
            .OrderByDescending(e => e.Id)
            .Select(e => (int?)e.JobId)
            .FirstOrDefaultAsync();
        Flash = TempData["Flash"] as string;
        return true;
    }

    public bool HasKind(VendorDocumentKind kind) => Vendor.Documents.Any(d => d.Kind == kind);
}
