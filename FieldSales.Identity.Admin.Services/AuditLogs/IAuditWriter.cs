namespace FieldSales.Identity.Services.AuditLogs;

public interface IAuditWriter
{
    Task WriteAsync(AdminAuditEvent auditEvent, CancellationToken cancellationToken = default);
}