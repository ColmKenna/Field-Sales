using FieldSales.Directory.Contracts;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Directory;
using FieldSales.Web.Presentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Customers;

/// <summary>The Customer record (H-25): its Locations, and the Pricing section (H-21, WI-063) with the tiers it holds and a sample
/// of what ticked tiers would mean for its prices, recalculated by "Update sample" before anything is saved.</summary>
public sealed class DetailModel(DirectoryApiClient directory, CatalogueApiClient catalogue) : PageModel
{
    public CustomerDetails Customer { get; private set; } = null!;
    /// <summary>Null when pricing couldn't be read; the rest of the record still shows.</summary>
    public CustomerTiersDetails? Tiers { get; private set; }
    public CustomerPriceSample? Sample { get; private set; }

    /// <summary>The ticked tiers: the held ones when the page opens, or as ticked when the sample is updated or a save is refused.</summary>
    [BindProperty] public List<Guid> TierIds { get; set; } = [];
    /// <summary>A product to check in the sample.</summary>
    [BindProperty] public string? Find { get; set; }

    /// <summary>The names of the tiers the customer holds, as saved.</summary>
    public IReadOnlyList<string> HeldNames => Tiers is null ? []
        : [.. Tiers.Tiers.Where(tier => Tiers.HeldTierIds.Contains(tier.Id)).Select(tier => tier.Name)];

    /// <summary>Whether the sample shows tiers other than the ones saved.</summary>
    public bool Unsaved => Tiers is not null && !TierIds.ToHashSet().SetEquals(Tiers.HeldTierIds);

    public Task<IActionResult> OnGetAsync(Guid id) => ShowAsync(id, ticked: false);

    /// <summary>Recalculates the sample with the ticked tiers and any product searched for; saves nothing (H21.1).</summary>
    public Task<IActionResult> OnPostSampleAsync(Guid id) => ShowAsync(id, ticked: true);

    public async Task<IActionResult> OnPostTiersAsync(Guid id)
    {
        PriceTierSaveResult result = await catalogue.SaveCustomerTiersAsync(id, TierIds, HttpContext.RequestAborted);
        switch (result.Status)
        {
            case PriceTierSaveStatus.Saved: return RedirectToPage("Detail", new { id });
            case PriceTierSaveStatus.Missing: return NotFound();
            case PriceTierSaveStatus.Invalid when result.Errors is not null:
                foreach (string message in result.Errors.Values.SelectMany(messages => messages)) ModelState.AddModelError(string.Empty, message);
                return await ShowAsync(id, ticked: true);
            default: return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<IActionResult> ShowAsync(Guid id, bool ticked)
    {
        var customer = await directory.CustomerAsync(id, HttpContext.RequestAborted);
        if (!customer.Success) return StatusCode((int)customer.Status);
        Customer = customer.Value!;
        // Pricing is one section of the record: if it can't be read, the section says so and the rest still shows.
        var tiers = await catalogue.ReadCustomerTiersAsync(id, HttpContext.RequestAborted);
        if (!tiers.Found) return Page();
        Tiers = tiers.Value!;
        if (!ticked) TierIds = [.. Tiers.HeldTierIds];
        var sample = await catalogue.CustomerPriceSampleAsync(id, TierIds, Find, HttpContext.RequestAborted);
        Sample = sample.Found ? sample.Value : null;
        return Page();
    }
}
