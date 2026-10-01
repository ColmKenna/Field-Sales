using System.ComponentModel.DataAnnotations;
using FieldSales.Identity.Services.ApiScopes;
using FieldSales.Identity.Services.Scopes;
using FieldSales.Identity.Services.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.ApiScopes;

public class EditInputModel
{
    [StringLength(ValidationConstants.MaxDisplayNameLength)]
    [Display(Name = "Display Name")]
    public string? DisplayName { get; set; }

    [StringLength(ValidationConstants.MaxDescriptionLength)] public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public bool Required { get; set; }

    public bool Emphasize { get; set; }

    [Display(Name = "Show in Discovery Document")]
    public bool ShowInDiscoveryDocument { get; set; } = true;
}

public class EditModel(IApiScopeEditorService apiScopeEditorService) : PageModel
{
    private readonly IApiScopeEditorService _apiScopeEditorService = apiScopeEditorService;

    // Bound from the query string only (never form body) so a tampered hidden/posted
    // field can never redirect a save/claim edit onto a different scope's row.
    [FromQuery] public string Name { get; set; } = string.Empty;

    [BindProperty] public EditInputModel Input { get; set; } = new();

    public ApiScopeEditorModel Editor { get; private set; } = new()
    {
        Name = string.Empty,
        DisplayName = null,
        Description = null,
        Claims = []
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return NotFound();

        ApiScopeEditorModel? editor =
            await _apiScopeEditorService.GetForEditAsync(ScopeName.Create(Name), cancellationToken);
        if (editor is null)
            return NotFound();

        Editor = editor;
        Input = new EditInputModel
        {
            DisplayName = editor.DisplayName,
            Description = editor.Description,
            Enabled = editor.Enabled,
            Required = editor.Required,
            Emphasize = editor.Emphasize,
            ShowInDiscoveryDocument = editor.ShowInDiscoveryDocument
        };

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return NotFound();

        if (!ModelState.IsValid)
        {
            ApiScopeEditorModel? editor =
                await _apiScopeEditorService.GetForEditAsync(ScopeName.Create(Name), cancellationToken);
            if (editor is null)
                return NotFound();
            Editor = editor;
            return Page();
        }

        bool success = await _apiScopeEditorService.UpdateBasicsAsync(
            new UpdateApiScopeBasicsCommand(
                ScopeName.Create(Name),
                Input.DisplayName,
                Input.Description,
                Input.Enabled,
                Input.Required,
                Input.Emphasize,
                Input.ShowInDiscoveryDocument),
            cancellationToken);
        if (!success)
            return NotFound();

        return RedirectToPage(new { name = Name });
    }

    public async Task<IActionResult> OnPostAddClaimAsync([FromQuery] string name, string claimType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return NotFound();

        bool success = await _apiScopeEditorService.AddClaimAsync(ScopeName.Create(name), ClaimType.Create(claimType),
            cancellationToken);
        if (!success)
            return NotFound();

        return RedirectToPage(new { name });
    }

    public async Task<IActionResult> OnPostRemoveClaimAsync([FromQuery] string name, string claimType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return NotFound();

        bool success = await _apiScopeEditorService.RemoveClaimAsync(ScopeName.Create(name),
            ClaimType.Create(claimType), cancellationToken);
        if (!success)
            return NotFound();

        return RedirectToPage(new { name });
    }
}