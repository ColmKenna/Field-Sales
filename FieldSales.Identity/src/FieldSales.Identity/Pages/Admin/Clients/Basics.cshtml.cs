using FieldSales.Identity.Presentation;
using System.ComponentModel.DataAnnotations;
using FieldSales.Identity.Services.Clients;
using FieldSales.Identity.Services.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Clients;

public class BasicsInputModel
{
    [Required(ErrorMessage = "Client Name is required")]
    [StringLength(ValidationConstants.MaxNameLength)]
    [Display(Name = "Client Name")]
    public string ClientName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    [StringLength(ValidationConstants.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class BasicsModel(IClientOverviewService clientOverviewService) : PageModel
{
    private readonly IClientOverviewService _clientOverviewService = clientOverviewService;

    [BindProperty(SupportsGet = true)] public string Id { get; set; } = string.Empty;

    [BindProperty] public BasicsInputModel Input { get; set; } = new();

    public string ClientIdDisplay { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Id))
            return NotFound();

        ClientDetailsModel? client =
            await _clientOverviewService.GetClientDetailsAsync(ClientId.Create(Id), cancellationToken);
        if (client is null)
            return NotFound();

        ClientIdDisplay = client.ClientId;
        Input.ClientName = client.ClientName;
        Input.Description = client.Description;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Id))
            return NotFound();

        if (!ModelState.IsValid)
        {
            ClientDetailsModel? client =
                await _clientOverviewService.GetClientDetailsAsync(ClientId.Create(Id), cancellationToken);
            if (client is null)
                return NotFound();
            ClientIdDisplay = client.ClientId;
            return Page();
        }

        AdminMutationResult result = await _clientOverviewService.UpdateClientBasicsAsync(ClientId.Create(Id),
            Input.ClientName, Input.Description, cancellationToken);
        if (result.Status == AdminMutationStatus.NotFound)
            return NotFound();

        if (!result.Succeeded)
        {
            MapErrors(result);
            ClientIdDisplay = Id;
            return Page();
        }

        return RedirectToPage("./Details", new { id = Id });
    }

    private void MapErrors(AdminMutationResult result)
    {
        ModelState.AddErrors(result.Errors);
    }
}