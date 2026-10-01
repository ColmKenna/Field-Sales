using FieldSales.Web.Presentation;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FieldSales.Quantities;

namespace FieldSales.Web.Pages.HeadOffice.Products;

public sealed class CreateModel(CatalogueApiClient catalogue) : PageModel
{
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public Guid? CategoryId { get; set; }
    [BindProperty] public decimal? BasePrice { get; set; }
    [BindProperty] public string Unit { get; set; } = "Each";
    [BindProperty] public decimal? QuantityStep { get; set; }
    [BindProperty] public decimal? MinimumQuantity { get; set; }
    public IReadOnlyList<ProductCategoryChoice> Categories { get; private set; } = [];

    public Task<IActionResult> OnGetAsync() => ShowFormAsync();

    public Task<IActionResult> OnPostPreviewAsync()
    {
        ProductUnitForm.ValidatePreview(ModelState, Unit);
        return ShowFormAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ProductUnitForm normalized = ProductUnitForm.Normalize(ModelState, Unit, QuantityStep, MinimumQuantity, useLegacyEach: !Request.Form.ContainsKey(nameof(Unit)));
        Unit = normalized.Unit;
        QuantityStep = normalized.Step;
        MinimumQuantity = normalized.Minimum;
        if (!ModelState.IsValid) return await ShowFormAsync();
        CreateProductResult result = await catalogue.CreateProductAsync(Code, Name, CategoryId,
            BasePrice, HttpContext.RequestAborted, Unit, QuantityStep, MinimumQuantity);
        if (result.Status == CreateProductStatus.Created && result.Product is not null)
            return RedirectToPage("Detail", new { id = result.Product.Id });
        if (result.Status != CreateProductStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        ModelState.AddErrors(result.Errors);
        return await ShowFormAsync();
    }

    private async Task<IActionResult> ShowFormAsync()
    {
        IReadOnlyList<ProductCategoryChoice>? categories = await catalogue.ProductCategoriesAsync(HttpContext.RequestAborted);
        if (categories is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        Categories = categories;
        return Page();
    }
}
