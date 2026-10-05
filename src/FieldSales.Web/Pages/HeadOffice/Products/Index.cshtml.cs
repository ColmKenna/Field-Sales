using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Products;

public sealed class IndexModel(CatalogueApiClient catalogue) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BrandId { get; set; }
    public ProductSearchResponse Results { get; private set; } = new([], [], []);
    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid) return BadRequest();
        Query = Query?.Trim() ?? string.Empty;
        var read = await catalogue.SearchProductsAsync(Query, CategoryId, BrandId, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        Results = read.Value!;
        return Page();
    }
}
