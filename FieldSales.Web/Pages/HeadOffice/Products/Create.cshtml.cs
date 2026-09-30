using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Products;

public sealed class CreateModel(CatalogueApiClient catalogue) : PageModel
{
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public Guid? CategoryId { get; set; }
    [BindProperty] public decimal? BasePrice { get; set; }
    public IReadOnlyList<ProductCategoryChoice> Categories { get; private set; } = [];

    public Task<IActionResult> OnGetAsync() => ShowFormAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return await ShowFormAsync();
        CreateProductResult result = await catalogue.CreateProductAsync(Code, Name, CategoryId,
            BasePrice, HttpContext.RequestAborted);
        if (result.Status == CreateProductStatus.Created && result.Product is not null)
            return RedirectToPage("Detail", new { id = result.Product.Id });
        if (result.Status != CreateProductStatus.Invalid || result.Errors is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        foreach ((string field, string[] errors) in result.Errors)
            foreach (string error in errors) ModelState.AddModelError(field, error);
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
