using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;

namespace ProposalEval.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<RfqRow> Rfqs { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Rfqs = await db.Rfqs
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RfqRow(
                r.Id,
                r.Number,
                r.Title,
                r.Status,
                r.Vendors.Count,
                r.Documents.Count(d => d.Kind == RfqDocumentKind.Tor) > 0,
                r.Documents.Count(d => d.Kind == RfqDocumentKind.ScoringStrategy) > 0))
            .ToListAsync();
    }

    public sealed record RfqRow(
        int Id,
        string Number,
        string Title,
        RfqStatus Status,
        int VendorCount,
        bool HasTor,
        bool HasStrategy);
}
