using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.Services;
using FieldSales.Identity.Services.AuditLogs;
using Microsoft.AspNetCore.Http;

namespace FieldSales.Identity.Services.Users;

public sealed partial class UserDetailsService(
    IIdentityUserAdministrationStore store,
    PersistedGrantDbContext persistedGrantDbContext,
    ReservedClaimTypePolicy reservedClaimTypes,
    IBackChannelLogoutService backChannelLogoutService,
    IAuditWriter auditWriter,
    IHttpContextAccessor httpContextAccessor) : IUserDetailsService
{
    private readonly CategoryAudit audit = AuditOperation.For(auditWriter, AuditCategory.User);
    private readonly IAuditWriter auditWriter = auditWriter;
    private readonly IBackChannelLogoutService backChannelLogoutService = backChannelLogoutService;
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;
    private readonly PersistedGrantDbContext persistedGrantDbContext = persistedGrantDbContext;
    private readonly ReservedClaimTypePolicy reservedClaimTypes = reservedClaimTypes;
    private readonly IIdentityUserAdministrationStore store = store;
}
