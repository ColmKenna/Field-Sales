using FieldSales.Identity.Presentation;
using System.ComponentModel.DataAnnotations;
using FieldSales.Identity.Services.Clients;
using FieldSales.Identity.Services.SecretReveals;
using FieldSales.Identity.Services.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Clients;

public class SecretsModel(
    IClientSecretsService clientSecretsService,
    ISecretRevealService secretRevealService) : PageModel
{
    private readonly IClientSecretsService _clientSecretsService = clientSecretsService;
    private readonly ISecretRevealService _secretRevealService = secretRevealService;

    [BindProperty(SupportsGet = true)] public string Id { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(ValidationConstants.MaxClientSecretDescriptionLength)]
    public string? Description { get; set; }

    [BindProperty]
    [Display(Name = "Expiration Date")]
    public DateTime? Expiration { get; set; }

    public string? GeneratedSecret { get; set; }

    [TempData(Key = SecretRevealPresentation.HandleKey)] public string? SecretRevealHandle { get; set; }

    [TempData] public string? RevokeErrorMessage { get; set; }

    public ClientSecretsModel Client { get; private set; } = default!;

    public bool RevealMode => !string.IsNullOrEmpty(GeneratedSecret);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!FormPresentation.TryGetClientId(Id, out ClientId clientId))
            return NotFound();

        GeneratedSecret = await _secretRevealService.TryRevealAsync(
            new SecretRevealTarget(SecretRevealPurpose.ClientSecretGenerated, Id), SecretRevealHandle, cancellationToken);

        return await LoadPageAsync(clientId, cancellationToken);
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        if (!FormPresentation.TryGetClientId(Id, out ClientId clientId))
            return NotFound();

        if (!ModelState.IsValid)
            return await LoadPageAsync(clientId, cancellationToken);

        ClientSecretGenerateResult result =
            await _clientSecretsService.GenerateClientSecretAsync(clientId, Description, Expiration,
                cancellationToken);
        if (!result.Success)
        {
            if (result.Status == AdminMutationStatus.NotFound)
                return NotFound();

            ModelState.AddErrors(result.Errors);

            return await LoadPageAsync(clientId, cancellationToken);
        }

        if (!string.IsNullOrEmpty(result.PlaintextSecret))
        {
            SecretRevealHandle = await _secretRevealService.IssueHandleAsync(
                new SecretRevealTarget(SecretRevealPurpose.ClientSecretGenerated, Id),
                result.PlaintextSecret,
                cancellationToken);
        }

        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostRevokeAsync(int secretId, CancellationToken cancellationToken)
    {
        if (!FormPresentation.TryGetClientId(Id, out ClientId clientId))
            return NotFound();

        ClientSecretRevokeResult result =
            await _clientSecretsService.RevokeClientSecretAsync(clientId, secretId, cancellationToken);
        if (!result.Success)
        {
            if (result.Status == AdminMutationStatus.NotFound)
                return NotFound();

            RevokeErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult> LoadPageAsync(ClientId clientId, CancellationToken cancellationToken)
    {
        ClientSecretsModel? secrets =
            await _clientSecretsService.GetClientSecretsAsync(clientId, cancellationToken);
        if (secrets is null)
            return NotFound();

        Client = secrets;
        return Page();
    }
}
