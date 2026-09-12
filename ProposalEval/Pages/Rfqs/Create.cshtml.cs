using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProposalEval.Data;

namespace ProposalEval.Pages.Rfqs;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var now = DateTime.UtcNow;
        var rfq = new Rfq
        {
            Number = Input.Number.Trim(),
            Title = Input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            Status = RfqStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Rfqs.Add(rfq);
        await db.SaveChangesAsync();
        return RedirectToPage("Details", new { id = rfq.Id });
    }

    public sealed class InputModel
    {
        [Required, StringLength(64)]
        public string Number { get; set; } = "";

        [Required, StringLength(500)]
        public string Title { get; set; } = "";

        [StringLength(4000)]
        public string? Description { get; set; }
    }
}
