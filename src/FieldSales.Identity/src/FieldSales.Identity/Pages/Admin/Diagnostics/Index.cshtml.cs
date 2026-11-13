using FieldSales.Identity.Services.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Diagnostics;

public sealed class IndexModel(IDiagnosticsService diagnosticsService) : PageModel
{
    private readonly IDiagnosticsService diagnosticsService = diagnosticsService;

    public DiagnosticsModel Diagnostics { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Diagnostics = await diagnosticsService.GetDiagnosticsAsync(cancellationToken);
}