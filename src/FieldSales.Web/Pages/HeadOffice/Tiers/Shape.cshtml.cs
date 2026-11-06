using System.ComponentModel.DataAnnotations;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Presentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Tiers;

/// <summary>H-20's "Change shape…" confirm step (WI-061 D2): says what stops applying before anything changes, then changes the
/// tier to its other shape on Confirm. To a price list, percentage overrides are removed and named, fixed ones stay as listed prices
/// (D1). To a percentage tier, the new percentage is entered first, then the warning names it, and listed prices stay as fixed
/// overrides (D4).</summary>
public sealed class ShapeModel(CatalogueApiClient catalogue) : PageModel
{
    public PriceTierDetails Tier { get; private set; } = null!;

    /// <summary>The new default percentage, when changing a price list to a percentage tier.</summary>
    [BindProperty(SupportsGet = true), Display(Name = "Percentage off base")]
    [Range(typeof(decimal), "0.01", "99.99", ErrorMessage = "Enter a percentage more than 0 and less than 100, with up to two decimal places.")]
    public decimal? PercentOff { get; set; }

    public bool ToPriceList => Tier.Shape == PriceTierShapes.Percentage;

    /// <summary>Whether the warning and Confirm show: straight away for a price list; for a percentage tier once a valid percentage is
    /// entered, so the warning can name it.</summary>
    public bool Reviewing => ToPriceList || (PercentOff is not null && ModelState.IsValid);

    /// <summary>The percentage overrides a change to a price list removes.</summary>
    public IReadOnlyList<PriceTierOverrideItem> Removed => [.. Tier.Overrides.Where(entry => entry.PercentOff is not null)];

    /// <summary>The fixed prices kept through the change: as listed prices, or back as fixed overrides.</summary>
    public IReadOnlyList<PriceTierOverrideItem> Kept => [.. Tier.Overrides.Where(entry => entry.FixedPrice is not null)];

    public Task<IActionResult> OnGetAsync(Guid id) => ShowAsync(id);

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (await ReadAsync(id) is { } failure) return failure;
        if (!ToPriceList && !ModelState.IsValid) return Page();
        PriceTierSaveResult result = await catalogue.ChangePriceTierShapeAsync(id,
            ToPriceList ? PriceTierShapes.PriceList : PriceTierShapes.Percentage, ToPriceList ? null : PercentOff, HttpContext.RequestAborted);
        switch (result.Status)
        {
            case PriceTierSaveStatus.Saved: return RedirectToPage("Detail", new { id });
            case PriceTierSaveStatus.Missing: return NotFound();
            case PriceTierSaveStatus.Invalid when result.Errors is not null:
                // A refused percentage goes back to its box; anything else (the tier changed shape meanwhile) to the summary.
                foreach ((string field, string[] messages) in result.Errors)
                foreach (string message in messages)
                    ModelState.AddModelError(field == nameof(PercentOff) && !ToPriceList ? nameof(PercentOff) : string.Empty, message);
                return Page();
            default: return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<IActionResult> ShowAsync(Guid id) => await ReadAsync(id) ?? Page();

    private async Task<IActionResult?> ReadAsync(Guid id)
    {
        var read = await catalogue.ReadPriceTierAsync(id, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        Tier = read.Value!;
        return null;
    }
}
