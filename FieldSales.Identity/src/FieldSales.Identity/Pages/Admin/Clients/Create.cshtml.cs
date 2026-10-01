using FieldSales.Identity.Presentation;
using System.Text.Json;
using FieldSales.Identity.Services.Clients;
using FieldSales.Identity.Services.SecretReveals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Clients;

public class CreateModel(
    IClientCreateService clientCreateService,
    IClientPresetService clientPresetService,
    ISecretRevealService secretRevealService) : PageModel
{
    private readonly IClientCreateService _clientCreateService = clientCreateService;
    private readonly IClientPresetService _clientPresetService = clientPresetService;
    private readonly ISecretRevealService _secretRevealService = secretRevealService;

    public static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [BindProperty] public ClientCreateInputModel Input { get; set; } = new();

    public string? CreatedSecret { get; set; }

    public string? CreatedClientId { get; set; }

    [TempData] public string? SecretRevealHandle { get; set; }

    public bool RevealMode => !string.IsNullOrEmpty(CreatedSecret);

    public List<string> AvailableScopes { get; set; } = new();

    public IReadOnlyList<ClientPreset> AvailablePresets { get; private set; } = Array.Empty<ClientPreset>();

    public async Task OnGetAsync(string? clientId, CancellationToken cancellationToken)
    {
        AvailablePresets = _clientPresetService.GetAvailablePresets();
        await LoadAvailableScopesAsync(cancellationToken);

        string? handle = SecretRevealHandle;
        if (!string.IsNullOrEmpty(handle) && !string.IsNullOrWhiteSpace(clientId))
        {
            SecretRevealConsumeResult reveal = await _secretRevealService.ConsumeAsync(
                new SecretRevealTarget(SecretRevealPurpose.ClientCreated, clientId),
                Services.SecretReveals.SecretRevealHandle.Create(handle), cancellationToken);
            if (reveal.Status == SecretRevealConsumeStatus.Revealed)
                CreatedSecret = reveal.Plaintext;
        }

        CreatedClientId = clientId;

        if (string.IsNullOrWhiteSpace(Input.ClientId) && string.IsNullOrWhiteSpace(CreatedClientId))
            ApplyPresetDefaults(ClientPresetIds.Web);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            AvailablePresets = _clientPresetService.GetAvailablePresets();
            await LoadAvailableScopesAsync(cancellationToken);
            return Page();
        }

        ClientCreateResult result = await _clientCreateService.CreateClientAsync(Input, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddErrors(result.Errors, "Input");

            if (result.Errors.Count == 0)
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create client.");
            AvailablePresets = _clientPresetService.GetAvailablePresets();
            await LoadAvailableScopesAsync(cancellationToken);
            return Page();
        }

        if (!string.IsNullOrEmpty(result.PlaintextSecret))
        {
            SecretRevealTicket ticket = await _secretRevealService.IssueAsync(
                new SecretRevealTarget(SecretRevealPurpose.ClientCreated, result.ClientId!),
                result.PlaintextSecret,
                cancellationToken);
            SecretRevealHandle = ticket.Handle;
        }

        return RedirectToPage("./Create", new { clientId = result.ClientId });
    }

    private void ApplyPresetDefaults(string presetId)
    {
        Input.SelectedPreset = presetId;
        ClientPreset? preset = _clientPresetService.GetPreset(presetId) ?? _clientPresetService.GetPreset(ClientPresetIds.Web);

        if (preset is not null)
        {
            Input.RequirePkce = preset.RequirePkce;
            Input.RequireClientSecret = preset.RequireClientSecret;
            Input.GrantTypes = preset.GrantTypes.ToList();
            Input.AllowedScopes = preset.AllowedScopes.ToList();
        }
    }

    private async Task LoadAvailableScopesAsync(CancellationToken cancellationToken) =>
        AvailableScopes = await _clientCreateService.GetAvailableScopesAsync(cancellationToken);
}