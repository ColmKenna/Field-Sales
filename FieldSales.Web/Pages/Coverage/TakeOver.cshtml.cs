using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class TakeOverModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    [BindProperty] public string? GivingRepSubject { get; set; }
    public TransferSourceOptions Options { get; private set; } = new(new("", ""), []);

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadAsync() is { } denied) return denied;
        if (!Options.GivingReps.Any(row => row.Subject == GivingRepSubject))
        { ModelState.AddModelError(nameof(GivingRepSubject), "Choose a rep whose assignments you can transfer. Reload if their book changed."); return Page(); }
        return RedirectToPage("./Transfer", new { repSubject = GivingRepSubject, receivingRepSubject = RepSubject, pull = true });
    }
    private async Task<IActionResult?> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(RepSubject)) return BadRequest();
        var result = await coverage.TransferSourcesAsync(RepSubject, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Options = result.Value!; return null;
    }
}
