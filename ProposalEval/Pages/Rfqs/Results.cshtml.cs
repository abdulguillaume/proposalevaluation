using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProposalEval.Api;
using ProposalEval.Services;

namespace ProposalEval.Pages.Rfqs;

public class ResultsModel(RfqAppService rfqs) : PageModel
{
    public RankingDto Ranking { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var result = await rfqs.GetRankingAsync(id, cancellationToken);
        if (!result.Ok)
            return result.Status == 404 ? NotFound() : BadRequest();

        Ranking = result.Data!;
        return Page();
    }
}
