namespace FieldSales.Identity.Services.AuditLogs;

public sealed class AuditLogListService(IAdminAuditStore store) : IAuditLogListService
{
    private readonly IAdminAuditStore store = store;

    public Task<ListResult<AuditLogListItem>> GetAuditLogEntriesAsync(
        AuditLogFilter filter,
        Pagination pagination = default,
        CancellationToken cancellationToken = default) =>
        store.GetEntriesAsync(filter, pagination, cancellationToken);
}