using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Pages.Jobs;

public class ReviewModel(AppDbContext db) : PageModel
{
    public EvaluationJob Job { get; private set; } = null!;
    public ProposalSummary Summary { get; private set; } = null!;
    public bool CanReview { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var job = await db.EvaluationJobs
            .Include(j => j.Vendor)
            .Include(j => j.Rfq)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound();
        if (job.JobType != JobType.Summarize || job.Vendor is null)
            return NotFound();

        var summary = await db.ProposalSummaries
            .Where(s => s.JobId == job.Id)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null)
            return NotFound();

        Job = job;
        Summary = summary;
        CanReview = summary.Status == SummaryStatus.Draft;
        return Page();
    }
}
