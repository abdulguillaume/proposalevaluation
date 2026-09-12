using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;

namespace ProposalEval.Pages.Vendors;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int RfqId { get; set; }

    public string? RfqNumber { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var rfq = await db.Rfqs.FindAsync(RfqId);
        if (rfq is null)
            return NotFound();
        RfqNumber = rfq.Number;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var rfq = await db.Rfqs.Include(r => r.Documents).Include(r => r.Vendors).FirstOrDefaultAsync(r => r.Id == RfqId);
        if (rfq is null)
            return NotFound();

        RfqNumber = rfq.Number;
        if (!ModelState.IsValid)
            return Page();

        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            RfqId = rfq.Id,
            Name = Input.Name.Trim(),
            ExternalProjectId = string.IsNullOrWhiteSpace(Input.ExternalProjectId) ? null : Input.ExternalProjectId.Trim(),
            Status = VendorStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Vendors.Add(vendor);

        if (rfq.Status is not RfqStatus.Evaluating and not RfqStatus.Completed
            && rfq.Documents.Any(d => d.Kind == RfqDocumentKind.Tor)
            && rfq.Documents.Any(d => d.Kind == RfqDocumentKind.ScoringStrategy))
        {
            rfq.Status = RfqStatus.Ready;
            rfq.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        return RedirectToPage("Details", new { id = vendor.Id });
    }

    public sealed class InputModel
    {
        [Required, StringLength(256)]
        public string Name { get; set; } = "";

        [Display(Name = "Project ID"), StringLength(64)]
        public string? ExternalProjectId { get; set; }
    }
}
