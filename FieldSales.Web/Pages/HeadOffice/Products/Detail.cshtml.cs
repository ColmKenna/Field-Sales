using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Products;

public sealed class DetailModel(CatalogueApiClient catalogue) : PageModel
{
    public ProductDetails Details { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ProductDetails? details = await catalogue.ProductDetailsAsync(id, HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Details = details;
        return Page();
    }
}
