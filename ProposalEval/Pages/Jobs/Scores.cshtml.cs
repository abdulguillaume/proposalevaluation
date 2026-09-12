using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;
using ProposalEval.Services;

namespace ProposalEval.Pages.Jobs;

public class ScoresModel(AppDbContext db) : PageModel
{
    public EvaluationJob Job { get; private set; } = null!;
    public VendorEvaluation Evaluation { get; private set; } = null!;
    public ProposalSummary? Summary { get; private set; }
    public bool CanReview { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var job = await db.EvaluationJobs
            .Include(j => j.Vendor)
            .Include(j => j.Rfq)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null || job.Vendor is null)
            return NotFound();

        var evaluation = await db.VendorEvaluations
            .Include(e => e.Scores)
            .ThenInclude(s => s.Criterion)
            .FirstOrDefaultAsync(e => e.JobId == id, cancellationToken);

        if (evaluation is null)
            return NotFound();

        Job = job;
        Evaluation = evaluation;
        CanReview = evaluation.Status == ScoreStatus.Draft;
        Summary = await db.ProposalSummaries
            .Where(s => s.VendorId == job.VendorId && s.Status == SummaryStatus.Accepted)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return Page();
    }
}
