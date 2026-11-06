using FieldSales.Web.Presentation;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FieldSales.Quantities;

namespace FieldSales.Web.Pages.HeadOffice.Products;

/// <summary>One quantity break row on the product record (WI-062, H-22): either value may be blank while editing.</summary>
public sealed class QuantityBreakInputRow
{
    public decimal? FromQuantity { get; set; }
    public decimal? Price { get; set; }
}

public sealed class DetailModel(CatalogueApiClient catalogue) : PageModel
{
    public ProductDetails Details { get; private set; } = null!;
    public ProductPriceItem? UpcomingPrice { get; private set; }
    public IReadOnlyList<ProductPriceItem> PriceHistory { get; private set; } = [];
    [BindProperty] public decimal? BasePrice { get; set; }
    [BindProperty] public DateOnly? EffectiveFrom { get; set; }
    [BindProperty] public string Unit { get; set; } = "Each";
    [BindProperty] public decimal? QuantityStep { get; set; }
    [BindProperty] public decimal? MinimumQuantity { get; set; }
    [BindProperty] public ProductClassificationInput? Classification { get; set; }
    [BindProperty] public ProductAttributeInput? Attribute { get; set; }
    /// <summary>The breaks being edited: the saved ones by quantity, or the rows as posted when a save is refused (WI-062 D2).</summary>
    [BindProperty] public List<QuantityBreakInputRow> Breaks { get; set; } = [];
    public IReadOnlyList<ProductReferenceItem> AttributeNameChoices { get; private set; } = [];
    public IReadOnlyList<ProductReferenceItem> ProfileChoices { get; private set; } = [];
    public IReadOnlyList<ProductReferenceItem> BrandChoices { get; private set; } = [];
    public IReadOnlyList<ProductReferenceItem> SupplierChoices { get; private set; } = [];
    public IReadOnlyList<ProductReferenceItem> RestrictionGroupChoices { get; private set; } = [];

    public Task<IActionResult> OnGetAsync(Guid id) => ShowAsync(id, populate: true);

    public async Task<IActionResult> OnPostAttributeAsync(Guid id)
    {
        // Each record form validates only the fields it submits.
        foreach (string key in ModelState.Keys.Where(key => key != nameof(Attribute)
                     && !key.StartsWith(nameof(Attribute) + ".", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
        Attribute ??= new();
        if (!ModelState.IsValid) return await ShowAsync(id, populate: true);
        var result = await catalogue.AddProductAttributeAsync(id, new(Attribute.NameId, Attribute.Value),
            HttpContext.RequestAborted);
        if (result.Status == AddProductAttributeStatus.Saved) return RedirectToPage("Detail", new { id });
        if (result.Status == AddProductAttributeStatus.Missing) return NotFound();
        if (result.Status != AddProductAttributeStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors, nameof(Attribute));
        return await ShowAsync(id, populate: true);
    }

    public async Task<IActionResult> OnPostClassificationAsync(Guid id)
    {
        ModelState.Remove(nameof(Unit));
        ModelState.Remove(nameof(QuantityStep));
        ModelState.Remove(nameof(MinimumQuantity));
        ModelState.Remove(nameof(BasePrice));
        ModelState.Remove(nameof(EffectiveFrom));
        Classification ??= new();
        if (!ModelState.IsValid) return await ShowAsync(id, populate: true);
        var result = await catalogue.SetProductClassificationAsync(id, Classification.ToRequest(), HttpContext.RequestAborted);
        if (result.Status == SetProductClassificationStatus.Saved) return RedirectToPage("Detail", new { id });
        if (result.Status == SetProductClassificationStatus.Missing) return NotFound();
        if (result.Status != SetProductClassificationStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors, nameof(Classification));
        return await ShowAsync(id, populate: true);
    }

    public async Task<IActionResult> OnPostPriceAsync(Guid id)
    {
        // This form does not submit the unit form's fields. Validate only its own inputs.
        ModelState.Remove(nameof(Unit));
        ModelState.Remove(nameof(QuantityStep));
        ModelState.Remove(nameof(MinimumQuantity));
        if (!ModelState.IsValid) return await ShowAsync(id, populate: true);
        AddProductBasePriceResult result = await catalogue.AddProductBasePriceAsync(id, BasePrice,
            EffectiveFrom, HttpContext.RequestAborted);
        if (result.Status == AddProductBasePriceStatus.Created) return RedirectToPage("Detail", new { id });
        if (result.Status == AddProductBasePriceStatus.Missing) return NotFound();
        if (result.Status != AddProductBasePriceStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors);
        return await ShowAsync(id, populate: true);
    }

    /// <summary>Saves the breaks as a set (WI-062 D2). Blank rows are left out; <paramref name="remove"/> is the row whose Remove
    /// button was pressed. Refusals come back against the rows as they were on the form.</summary>
    public async Task<IActionResult> OnPostBreaksAsync(Guid id, int? remove)
    {
        foreach (string key in ModelState.Keys.Where(key => !key.StartsWith(nameof(Breaks) + "[", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
        if (!ModelState.IsValid) return await ShowAsync(id, populate: true, keepBreaks: true);
        if (remove is int removed && removed >= 0 && removed < Breaks.Count) Breaks.RemoveAt(removed);
        // Which form row each sent break came from, so the API's refusals land on the right row.
        int[] rows = [.. Enumerable.Range(0, Breaks.Count).Where(row => Breaks[row].FromQuantity is not null || Breaks[row].Price is not null)];
        QuantityBreakRequestItem[] sent = [.. rows.Select(row => new QuantityBreakRequestItem(Breaks[row].FromQuantity, Breaks[row].Price))];
        SaveQuantityBreaksResult result = await catalogue.SetProductQuantityBreaksAsync(id, sent, HttpContext.RequestAborted);
        if (result.Status == SaveQuantityBreaksStatus.Saved) return RedirectToPage("Detail", new { id });
        if (result.Status == SaveQuantityBreaksStatus.Missing) return NotFound();
        if (result.Status != SaveQuantityBreaksStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        foreach ((string field, string[] messages) in result.Errors)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(field, @"^Breaks\[(\d+)\]\.(\w+)$");
            // A refusal that isn't about one row (the breaks changed while saving) goes to the section's summary.
            string key = match.Success && int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) is int index
                && index < rows.Length ? $"{nameof(Breaks)}[{rows[index]}].{match.Groups[2].Value}" : string.Empty;
            foreach (string message in messages) ModelState.AddModelError(key, message);
        }
        return await ShowAsync(id, populate: true, keepBreaks: true);
    }

    /// <summary>The saved break at this row's quantity and price, for its never-applies warnings; null for a row not saved as shown.</summary>
    public ProductQuantityBreakItem? SavedBreak(QuantityBreakInputRow row) => (Details.QuantityBreaks ?? [])
        .FirstOrDefault(entry => entry.FromQuantity == row.FromQuantity && entry.Price == row.Price);

    public Task<IActionResult> OnPostPreviewUnitAsync(Guid id)
    {
        ProductUnitForm.ValidatePreview(ModelState, Unit);
        return ShowAsync(id);
    }

    public async Task<IActionResult> OnPostUnitAsync(Guid id)
    {
        ProductUnitForm normalized = ProductUnitForm.Normalize(ModelState, Unit, QuantityStep, MinimumQuantity);
        Unit = normalized.Unit;
        QuantityStep = normalized.Step;
        MinimumQuantity = normalized.Minimum;
        if (!ModelState.IsValid) return await ShowAsync(id);
        UpdateProductUnitResult result = await catalogue.UpdateProductUnitAsync(id, Unit, QuantityStep,
            MinimumQuantity, HttpContext.RequestAborted);
        if (result.Status == UpdateProductUnitStatus.Saved) return RedirectToPage("Detail", new { id });
        if (result.Status == UpdateProductUnitStatus.Missing) return NotFound();
        if (result.Status != UpdateProductUnitStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors);
        return await ShowAsync(id);
    }

    private async Task<IActionResult> ShowAsync(Guid id, bool populate = false, bool keepBreaks = false)
    {
        var read = await catalogue.ProductDetailsAsync(id, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        ProductDetails details = read.Value!;
        Details = details;
        AttributeNameChoices = details.AttributeNameChoices ?? [];
        Attribute ??= new();
        Classification ??= ProductClassificationInput.From(details);
        var choices = details.ClassificationChoices;
        ProfileChoices = Choices(choices?.Profiles, details.Profile is { } profile ? [profile] : [], [Classification.ProfileId]);
        SupplierChoices = Choices(choices?.Suppliers, details.Supplier is { } supplier ? [supplier] : [], [Classification.SupplierId]);
        RestrictionGroupChoices = Choices(choices?.RestrictionGroups,
            details.RestrictionGroup is { } group ? [group] : [], [Classification.RestrictionGroupId]);
        BrandChoices = Choices(choices?.Brands, details.Brands?.Select(brand =>
            new ProductReferenceItem(brand.Id, brand.Name, brand.IsArchived)).ToArray() ?? [],
            Classification.AlternativeBrandIds.Select(id => (Guid?)id).Append(Classification.PrimaryBrandId));
        PriceHistory = details.PriceHistory.OrderByDescending(price => price.EffectiveFrom).ToArray();
        if (!keepBreaks)
            Breaks = [.. (details.QuantityBreaks ?? []).OrderBy(entry => entry.FromQuantity)
                .Select(entry => new QuantityBreakInputRow { FromQuantity = entry.FromQuantity, Price = entry.Price })];
        // CurrentPrice is resolved by the API's business date; the next entry is therefore future.
        UpcomingPrice = PriceHistory.Where(price => details.CurrentPrice is null
                || price.EffectiveFrom > details.CurrentPrice.EffectiveFrom)
            .MinBy(price => price.EffectiveFrom);
        if (populate)
        {
            Unit = details.Product.Unit;
            QuantityStep = details.Product.QuantityStep;
            MinimumQuantity = details.Product.MinimumQuantity;
        }
        return Page();
    }

    private static IReadOnlyList<ProductReferenceItem> Choices(IReadOnlyList<ProductReferenceItem>? active,
        IEnumerable<ProductReferenceItem> saved, IEnumerable<Guid?> selected)
    {
        var choices = (active ?? []).Concat(saved).DistinctBy(item => item.Id).ToList();
        foreach (Guid id in selected.Where(id => id is not null).Select(id => id!.Value).Distinct())
            if (!choices.Any(item => item.Id == id)) choices.Add(new(id, "Selection unavailable", false));
        return choices.OrderBy(item => item.Name).ToArray();
    }
}
