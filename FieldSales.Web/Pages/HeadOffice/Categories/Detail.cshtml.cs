using FieldSales.Web.Presentation;
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

    [BindProperty]
    public string? RenameName { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id) => await ShowAsync(id);

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid) return await ShowAsync(id);
        CreateCategoryResult result = await catalogue.CreateAsync(Name, id, HttpContext.RequestAborted);
        if (result.Status == CreateCategoryStatus.Created && result.Category is not null)
            return RedirectToPage("Detail", new { id = result.Category.Id });
        if (result.Status == CreateCategoryStatus.ParentMissing) return NotFound();
        if (!ModelState.AddCreationError(result.Status, nameof(Name)))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return await ShowAsync(id);
    }

    public async Task<IActionResult> OnPostRenameAsync(Guid id)
    {
        // The create form's required Name field does not belong to this handler.
        ModelState.Remove(nameof(Name));
        if (string.IsNullOrWhiteSpace(RenameName))
            ModelState.AddModelError(nameof(RenameName), "Enter a category name.");
        else if (RenameName.Trim().Length > 200)
            ModelState.AddModelError(nameof(RenameName), "Use 200 characters or fewer.");
        if (!ModelState.IsValid) return await ShowAsync(id);

        RenameCategoryResult result = await catalogue.RenameAsync(id, RenameName!, HttpContext.RequestAborted);
        if (result.Status == RenameCategoryStatus.Renamed)
            return RedirectToPage("Detail", new { id });
        if (result.Status == RenameCategoryStatus.Missing) return NotFound();
        if (result.Status == RenameCategoryStatus.Duplicate)
            ModelState.AddModelError(nameof(RenameName), "A category with this name already exists here.");
        else if (result.Status == RenameCategoryStatus.Invalid)
            ModelState.AddModelError(nameof(RenameName), "Enter a category name of up to 200 characters.");
        else return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return await ShowAsync(id);
    }

    private async Task<IActionResult> ShowAsync(Guid id)
    {
        var read = await catalogue.DetailsAsync(id, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        CategoryDetails details = read.Value!;
        Details = details;
        if (!ModelState.ContainsKey(nameof(RenameName))) RenameName = details.Category.Name;
        return Page();
    }
}
