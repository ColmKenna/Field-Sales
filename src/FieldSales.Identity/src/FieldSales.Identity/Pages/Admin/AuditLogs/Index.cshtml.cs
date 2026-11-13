using FieldSales.Identity.Configuration;
using FieldSales.Identity.Services;
using FieldSales.Identity.Services.AuditLogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FieldSales.Identity.Pages.Admin.AuditLogs;

public class IndexModel : PageModel
{
    private readonly IAuditLogListService auditLogListService;
    private readonly AdminConsoleOptions options;

    public IndexModel(IAuditLogListService auditLogListService, IOptions<AdminConsoleOptions> options)
    {
        this.auditLogListService = auditLogListService;
        this.options = options.Value;
    }

    [BindProperty(SupportsGet = true)] public AuditLogFilter Filter { get; set; } = new();

    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public ListResult<AuditLogListItem> AuditLogEntries { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Pagination pagination = options.CreatePagination(PageNumber, out int pageNumber);
        PageNumber = pageNumber;

        AuditLogEntries = await auditLogListService.GetAuditLogEntriesAsync(Filter, pagination, cancellationToken);
    }

    public Dictionary<string, string?> GetRouteValues() => new()
    {
        ["Filter.ActorSubjectId"] = Filter.ActorSubjectId,
        ["Filter.TargetId"] = Filter.TargetId,
        ["Filter.Category"] = Filter.Category?.Value,
        ["Filter.Action"] = Filter.Action?.Value,
        ["Filter.Outcome"] = Filter.Outcome?.ToString(),
        ["Filter.CorrelationId"] = Filter.CorrelationId
    };
}
