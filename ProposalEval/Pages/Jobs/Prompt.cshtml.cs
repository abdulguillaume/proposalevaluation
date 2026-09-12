using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProposalEval.Api;
using ProposalEval.Services;

namespace ProposalEval.Pages.Jobs;

public class PromptModel(PreparedPromptService prompts) : PageModel
{
    public PreparedPromptDto? Prompt { get; private set; }
    public string? Error { get; private set; }
    public int RfqId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, string? agent, int? rfqId, CancellationToken cancellationToken)
    {
        RfqId = rfqId ?? 0;
        var result = await prompts.GetAsync(id, agent, cancellationToken);
        if (!result.Ok)
        {
            if (result.Status == 404)
                return NotFound();
            Error = result.Error;
            return Page();
        }

        Prompt = result.Data!;
        return Page();
    }
}
