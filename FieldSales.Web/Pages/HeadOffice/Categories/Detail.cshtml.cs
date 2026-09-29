using System.ComponentModel.DataAnnotations;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Categories;

public sealed class DetailModel(CatalogueApiClient catalogue) : PageModel
{
    public CategoryDetails Details { get; private set; } = null!;

    [BindProperty]
    [Required(ErrorMessage = "Enter a category name.")]
    [StringLength(200, ErrorMessage = "Use 200 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id) => await ShowAsync(id);

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid) return await ShowAsync(id);
        CreateCategoryResult result = await catalogue.CreateAsync(Name, id, HttpContext.RequestAborted);
        if (result.Status == CreateCategoryStatus.Created && result.Category is not null)
            return RedirectToPage("Detail", new { id = result.Category.Id });
        if (result.Status == CreateCategoryStatus.ParentMissing) return NotFound();
        if (result.Status == CreateCategoryStatus.Duplicate)
            ModelState.AddModelError(nameof(Name), "A category with this name already exists here.");
        else if (result.Status == CreateCategoryStatus.Invalid)
            ModelState.AddModelError(nameof(Name), "Enter a category name of up to 200 characters.");
        else return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return await ShowAsync(id);
    }

    private async Task<IActionResult> ShowAsync(Guid id)
    {
        CategoryDetails? details = await catalogue.DetailsAsync(id, HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Details = details;
        return Page();
    }
}
