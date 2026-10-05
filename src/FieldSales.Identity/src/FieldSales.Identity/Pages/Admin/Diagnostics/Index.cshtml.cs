using FieldSales.Identity.Services.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Admin.Diagnostics;

public class IndexModel(IDiagnosticsService diagnosticsService) : PageModel
{
    private readonly IDiagnosticsService _diagnosticsService = diagnosticsService;

    public DiagnosticsModel Diagnostics { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Diagnostics = await _diagnosticsService.GetDiagnosticsAsync(cancellationToken);
}