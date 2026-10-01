using System.ComponentModel.DataAnnotations;
using FieldSales.Identity.Services.ApiScopes;
using FieldSales.Identity.Services.Scopes;
using FieldSales.Identity.Services.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.ApiScopes;

public class CreateInputModel
{
    [Required(ErrorMessage = "Scope name is required")]
    [StringLength(ValidationConstants.MaxScopeNameLength, ErrorMessage = "Scope name must not exceed 200 characters")]
    [Display(Name = "Scope Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(ValidationConstants.MaxDisplayNameLength, ErrorMessage = "Display name must not exceed 200 characters")]
    [Display(Name = "Display Name")]
    public string? DisplayName { get; set; }

    [StringLength(ValidationConstants.MaxDescriptionLength, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }
}

public class CreateModel(IApiScopeEditorService apiScopeEditorService) : PageModel
{
    private readonly IApiScopeEditorService _apiScopeEditorService = apiScopeEditorService;

    [BindProperty] public CreateInputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(nameof(Input.Name), "Scope name is required");
            return Page();
        }

        AdminMutationResult result = await _apiScopeEditorService.CreateAsync(
            new CreateApiScopeCommand(
                ScopeName.Create(Input.Name),
                Input.DisplayName,
                Input.Description),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(Input.Name), result.ErrorMessage ?? "Failed to create scope.");
            return Page();
        }

        return RedirectToPage("./Edit", new { name = Input.Name });
    }
}