using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Keys;

public sealed class IndexModel(IKeyMaterialService keyMaterialService) : PageModel
{
    private readonly IKeyMaterialService keyMaterialService = keyMaterialService;

    public List<SecurityKeyInfo> Keys { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<SecurityKeyInfo> keys = await keyMaterialService.GetValidationKeysAsync(cancellationToken);
        Keys = keys.ToList();
    }
}