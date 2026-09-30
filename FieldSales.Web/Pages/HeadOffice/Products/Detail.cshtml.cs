using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FieldSales.Quantities;

namespace FieldSales.Web.Pages.HeadOffice.Products;

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

    public Task<IActionResult> OnGetAsync(Guid id) => ShowAsync(id, populate: true);

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
        foreach ((string field, string[] errors) in result.Errors)
            foreach (string error in errors) ModelState.AddModelError(field, error);
        return await ShowAsync(id, populate: true);
    }

    public Task<IActionResult> OnPostPreviewUnitAsync(Guid id)
    {
        if (!UnitOfMeasureExtensions.TryParse(Unit, out _))
            ModelState.AddModelError(nameof(Unit), "Choose Each, kg, litre or metre.");
        return ShowAsync(id);
    }

    public async Task<IActionResult> OnPostUnitAsync(Guid id)
    {
        if (Unit == "Each")
        {
            QuantityStep = MinimumQuantity = null;
            ModelState.Remove(nameof(QuantityStep));
            ModelState.Remove(nameof(MinimumQuantity));
        }
        if (!ModelState.IsValid) return await ShowAsync(id);
        UpdateProductUnitResult result = await catalogue.UpdateProductUnitAsync(id, Unit, QuantityStep,
            MinimumQuantity, HttpContext.RequestAborted);
        if (result.Status == UpdateProductUnitStatus.Saved) return RedirectToPage("Detail", new { id });
        if (result.Status == UpdateProductUnitStatus.Missing) return NotFound();
        if (result.Status != UpdateProductUnitStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        foreach ((string field, string[] errors) in result.Errors)
            foreach (string error in errors) ModelState.AddModelError(field, error);
        return await ShowAsync(id);
    }

    private async Task<IActionResult> ShowAsync(Guid id, bool populate = false)
    {
        ProductDetails? details = await catalogue.ProductDetailsAsync(id, HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Details = details;
        PriceHistory = details.PriceHistory.OrderByDescending(price => price.EffectiveFrom).ToArray();
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
}
