using System.ComponentModel.DataAnnotations;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Presentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Tiers;

public sealed class TierInput
{
    [Display(Name = "Tier name")] public string? Name { get; set; }
    [Display(Name = "Percentage off base")] public decimal? PercentOff { get; set; }
}

/// <summary>An override form: the product, whether it's a percentage off or a fixed price, and the value.</summary>
public sealed class OverrideInput
{
    public const string PercentKind = "percent";
    public const string FixedKind = "fixed";

    public Guid ProductId { get; set; }
    [Display(Name = "Override")] public string Kind { get; set; } = PercentKind;
    [Display(Name = "Value")] public decimal? Value { get; set; }
}

/// <summary>H-20's tier detail (WI-060): leads with the overrides (H20.1), each with its term, today's list price and what the
/// tier gives today (D6); adds overrides from a product search (D7); renames or re-percentages the tier (D5).</summary>
public sealed class DetailModel(CatalogueApiClient catalogue) : PageModel
{
    /// <summary>The most search matches listed at once.</summary>
    public const int MaximumMatches = 20;

    public PriceTierDetails Tier { get; private set; } = null!;
    [BindProperty(SupportsGet = true)] public string? Find { get; set; }
    public IReadOnlyList<ProductSearchItem> Matches { get; private set; } = [];
    public int MatchCount { get; private set; }
    [BindProperty] public TierInput? Terms { get; set; }
    [BindProperty] public OverrideInput? Override { get; set; }
    /// <summary>The product whose override form was refused, so its message shows beside it.</summary>
    public Guid? RefusedProduct { get; private set; }

    public bool HasOverride(Guid productId) => Tier.Overrides.Any(entry => entry.ProductId == productId);

    public Task<IActionResult> OnGetAsync(Guid id) => ShowAsync(id, populate: true);

    public async Task<IActionResult> OnPostTermsAsync(Guid id)
    {
        Keep(nameof(Terms));
        Terms ??= new();
        if (!ModelState.IsValid) return await ShowAsync(id, populate: false);
        PriceTierSaveResult result = await catalogue.ChangePriceTierAsync(id, Terms.Name, Terms.PercentOff, HttpContext.RequestAborted);
        if (Failed(result) is { } failure) return failure;
        if (result.Status == PriceTierSaveStatus.Saved) return RedirectToPage("Detail", new { id });
        ModelState.AddErrors(result.Errors!, nameof(Terms));
        return await ShowAsync(id, populate: false);
    }

    public async Task<IActionResult> OnPostOverrideAsync(Guid id)
    {
        Keep(nameof(Override));
        Override ??= new();
        if (!ModelState.IsValid) return await RefusedAsync(id);
        bool isFixed = Override.Kind == OverrideInput.FixedKind;
        PriceTierSaveResult result = await catalogue.SetPriceTierOverrideAsync(id, Override.ProductId,
            isFixed ? null : Override.Value, isFixed ? Override.Value : null, HttpContext.RequestAborted);
        if (Failed(result) is { } failure) return failure;
        if (result.Status == PriceTierSaveStatus.Saved) return RedirectToPage("Detail", new { id });
        // Whichever field the API names, the message belongs to the value just entered.
        foreach (string message in result.Errors!.Values.SelectMany(messages => messages))
            ModelState.AddModelError($"{nameof(Override)}.{nameof(OverrideInput.Value)}", message);
        return await RefusedAsync(id);
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, Guid productId)
    {
        Keep(string.Empty);
        PriceTierSaveResult result = await catalogue.RemovePriceTierOverrideAsync(id, productId, HttpContext.RequestAborted);
        // An override already removed elsewhere leaves the page as wanted.
        if (result.Status is PriceTierSaveStatus.Saved or PriceTierSaveStatus.Missing) return RedirectToPage("Detail", new { id });
        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private Task<IActionResult> RefusedAsync(Guid id)
    {
        RefusedProduct = Override!.ProductId;
        return ShowAsync(id, populate: true);
    }

    // Each form validates only the fields it submits.
    private void Keep(string prefix)
    {
        foreach (string key in ModelState.Keys.Where(key => prefix.Length == 0 || (key != prefix
                     && !key.StartsWith(prefix + ".", StringComparison.Ordinal))).Where(key => key != nameof(Find)).ToArray())
            ModelState.Remove(key);
    }

    private IActionResult? Failed(PriceTierSaveResult result) => result.Status switch
    {
        PriceTierSaveStatus.Missing => NotFound(),
        PriceTierSaveStatus.Invalid when result.Errors is not null => null,
        PriceTierSaveStatus.Saved => null,
        _ => StatusCode(StatusCodes.Status503ServiceUnavailable)
    };

    private async Task<IActionResult> ShowAsync(Guid id, bool populate)
    {
        var read = await catalogue.ReadPriceTierAsync(id, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        Tier = read.Value!;
        if (populate) Terms = new() { Name = Tier.Name, PercentOff = Tier.PercentOff };
        Find = Find?.Trim();
        if (!string.IsNullOrEmpty(Find))
        {
            var search = await catalogue.SearchProductsAsync(Find, null, null, HttpContext.RequestAborted);
            if (!search.Found) return search.FailureResult();
            MatchCount = search.Value!.Items.Count;
            Matches = [.. search.Value.Items.Take(MaximumMatches)];
        }
        return Page();
    }
}
