using System.ComponentModel.DataAnnotations;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Presentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Tiers;

/// <summary>H-20's tier list (H20.1): each tier's terms and how many products have overrides, with a form to create one.
/// Customer counts arrive with tier assignment (T-6.2.1).</summary>
public sealed class IndexModel(CatalogueApiClient catalogue) : PageModel
{
    public IReadOnlyList<PriceTierListItem> Tiers { get; private set; } = [];
    [BindProperty, Display(Name = "Tier name")] public string? Name { get; set; }
    [BindProperty, Display(Name = "Percentage off base")] public decimal? PercentOff { get; set; }

    public Task<IActionResult> OnGetAsync() => ShowAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return await ShowAsync();
        PriceTierSaveResult result = await catalogue.CreatePriceTierAsync(Name, PercentOff, HttpContext.RequestAborted);
        if (result is { Status: PriceTierSaveStatus.Saved, Id: Guid id }) return RedirectToPage("Detail", new { id });
        if (result.Status != PriceTierSaveStatus.Invalid || result.Errors is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors);
        return await ShowAsync();
    }

    private async Task<IActionResult> ShowAsync()
    {
        var read = await catalogue.ReadPriceTiersAsync(HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        Tiers = read.Value!;
        return Page();
    }
}
