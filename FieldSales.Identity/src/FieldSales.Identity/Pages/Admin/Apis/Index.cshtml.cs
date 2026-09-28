using FieldSales.Identity.Configuration;
using FieldSales.Identity.Services;
using FieldSales.Identity.Services.Apis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FieldSales.Identity.Pages.Admin.Apis;

public class IndexModel : PageModel
{
    private readonly IApiResourceListService _apiResourceListService;
    private readonly AdminConsoleOptions _options;

    public IndexModel(IApiResourceListService apiResourceListService, IOptions<AdminConsoleOptions> options)
    {
        _apiResourceListService = apiResourceListService;
        _options = options.Value;
    }

    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }

    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public ListResult<ApiResourceListItem> ApiResources { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var pagination = Pagination.From(PageNumber, _options.DefaultPageSize);
        PageNumber = pagination.PageNumber;

        ApiResources =
            await _apiResourceListService.GetApiResourcesAsync(new ListQuery(Filter, pagination), cancellationToken);
    }
}