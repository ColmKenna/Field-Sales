using FieldSales.Web.Presentation;
using System.ComponentModel.DataAnnotations;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Categories;

public sealed class IndexModel(CatalogueApiClient catalogue) : PageModel
{
    public IReadOnlyList<CategoryItem> Roots { get; private set; } = [];
    public IReadOnlyList<CategorySearchResult> Results { get; private set; } = [];
    public string Query { get; private set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter a category name.")]
    [StringLength(200, ErrorMessage = "Use 200 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? query)
    {
        Query = query?.Trim() ?? string.Empty;
        if (Query.Length > 0)
        {
            IReadOnlyList<CategorySearchResult>? results = await catalogue.SearchCategoriesAsync(Query, HttpContext.RequestAborted);
            if (results is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            Results = results;
            return Page();
        }
        IReadOnlyList<CategoryItem>? roots = await catalogue.RootsAsync(HttpContext.RequestAborted);
        if (roots is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        Roots = roots;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return await ShowFormAsync();
        CreateCategoryResult result = await catalogue.CreateAsync(Name, null, HttpContext.RequestAborted);
        if (result.Status == CreateCategoryStatus.Created && result.Category is not null)
            return RedirectToPage("Detail", new { id = result.Category.Id });
        if (!ModelState.AddCreationError(result.Status, nameof(Name)))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return await ShowFormAsync();
    }

    private async Task<IActionResult> ShowFormAsync()
    {
        IReadOnlyList<CategoryItem>? roots = await catalogue.RootsAsync(HttpContext.RequestAborted);
        if (roots is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        Roots = roots;
        return Page();
    }
}
