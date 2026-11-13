using FieldSales.Identity.Configuration;
using FieldSales.Identity.Services;
using FieldSales.Identity.Services.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FieldSales.Identity.Pages.Admin.Clients;

public class IndexModel : PageModel
{
    private readonly IClientListService clientListService;
    private readonly AdminConsoleOptions options;

    public IndexModel(IClientListService clientListService, IOptions<AdminConsoleOptions> options)
    {
        this.clientListService = clientListService;
        this.options = options.Value;
    }

    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }

    public ListResult<ClientListItem> Clients { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ListQuery query = options.CreateListQuery(Filter, PageNumber, out int pageNumber);
        PageNumber = pageNumber;

        Clients = await clientListService.GetClientsAsync(query, cancellationToken);
    }
}
