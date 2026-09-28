using FieldSales.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Security;

// The cookie middleware writes only an opaque ticket key to the browser. The serialized
// authentication ticket (including OIDC tokens) is encrypted before it reaches SQL Server.
public sealed class SqlTicketStore(
    IServiceScopeFactory scopeFactory,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider) : ITicketStore
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("StaffTicket.v1");

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, CancellationToken.None);
    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, CancellationToken.None);
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, CancellationToken.None);
    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        string key = Guid.NewGuid().ToString("N");
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        db.Tickets.Add(new StoredTicket
        {
            Key = key,
            ProtectedValue = _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            ExpiresUtc = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddHours(8)
        });
        await db.SaveChangesAsync(cancellationToken);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        StoredTicket? stored = await db.Tickets.FindAsync([key], cancellationToken);
        if (stored is null) return;
        stored.ProtectedValue = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        stored.ExpiresUtc = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddHours(8);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        StoredTicket? stored = await db.Tickets.FindAsync([key], cancellationToken);
        if (stored is null) return null;
        if (stored.ExpiresUtc <= timeProvider.GetUtcNow())
        {
            db.Tickets.Remove(stored);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        return TicketSerializer.Default.Deserialize(_protector.Unprotect(stored.ProtectedValue));
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        await db.Tickets.Where(ticket => ticket.Key == key).ExecuteDeleteAsync(cancellationToken);
    }
}

public sealed class TicketStoreCookieOptions(SqlTicketStore store)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name == CookieAuthenticationDefaults.AuthenticationScheme)
            options.SessionStore = store;
    }
}
