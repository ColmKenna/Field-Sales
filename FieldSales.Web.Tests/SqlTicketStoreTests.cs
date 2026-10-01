using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

public sealed class SqlTicketStoreTests
{
    [Fact]
    public async Task FallbackExpiry_ReadsNamedCookieOptionsDuringStoreAndRenewWithoutDependencyCycle()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        TestClock clock = new();
        await using ServiceProvider services = CreateServices(connection, clock);
        await CreateSchemaAsync(services);
        SqlTicketStore store = services.GetRequiredService<SqlTicketStore>();
        AuthenticationTicket ticket = CreateTicket("token");
        ticket.Properties.ExpiresUtc = null;
        string key = await store.StoreAsync(ticket, CancellationToken.None);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
            Assert.Equal(clock.GetUtcNow().AddHours(2), (await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Tickets.SingleAsync()).ExpiresUtc);
        Assert.Same(store, services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies").SessionStore);
        var cache = services.GetRequiredService<IOptionsMonitorCache<CookieAuthenticationOptions>>();
        cache.TryRemove("Cookies");
        cache.TryAdd("Cookies", new CookieAuthenticationOptions { ExpireTimeSpan = TimeSpan.FromHours(4) });
        clock.Now = clock.Now.AddMinutes(30);
        await store.RenewAsync(key, ticket, CancellationToken.None);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
            Assert.Equal(clock.GetUtcNow().AddHours(4), (await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Tickets.SingleAsync()).ExpiresUtc);
        ticket.Properties.ExpiresUtc = clock.GetUtcNow().AddMinutes(10);
        await store.RenewAsync(key, ticket, CancellationToken.None);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
            Assert.Equal(ticket.Properties.ExpiresUtc, (await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Tickets.SingleAsync()).ExpiresUtc);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task TokensStayProtectedInSqlAndRemovedSessionCannotBeRetrieved()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using ServiceProvider services = CreateServices(connection);
        await CreateSchemaAsync(services);
        SqlTicketStore store = services.GetRequiredService<SqlTicketStore>();

        string key = await store.StoreAsync(CreateTicket("sensitive-access-token"), CancellationToken.None);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            StoredTicket saved = await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>()
                .Tickets.SingleAsync();
            Assert.DoesNotContain("sensitive-access-token", Encoding.UTF8.GetString(saved.ProtectedValue));
            Assert.NotEqual("sensitive-access-token", key);
        }
        AuthenticationTicket? retrieved = await store.RetrieveAsync(key, CancellationToken.None);
        Assert.Equal("sensitive-access-token", retrieved?.Properties.GetTokenValue("access_token"));

        await store.RemoveAsync(key, CancellationToken.None);
        Assert.Null(await store.RetrieveAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredSessionIsRejectedAndDeleted()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using ServiceProvider services = CreateServices(connection);
        await CreateSchemaAsync(services);
        SqlTicketStore store = services.GetRequiredService<SqlTicketStore>();
        string key = await store.StoreAsync(CreateTicket("expired", DateTimeOffset.UtcNow.AddMinutes(-1)),
            CancellationToken.None);

        Assert.Null(await store.RetrieveAsync(key, CancellationToken.None));
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Tickets.ToListAsync());
    }

    private static ServiceProvider CreateServices(SqliteConnection connection, TimeProvider? clock = null)
    {
        ServiceCollection services = new();
        services.AddDbContext<StaffWebDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton(clock ?? TimeProvider.System);
        services.Configure<CookieAuthenticationOptions>("Cookies", options => options.ExpireTimeSpan = TimeSpan.FromHours(2));
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, TicketStoreCookieOptions>();
        services.AddSingleton<SqlTicketStore>();
        return services.BuildServiceProvider();
    }

    private static async Task CreateSchemaAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Database.EnsureCreatedAsync();
    }

    private static AuthenticationTicket CreateTicket(string token, DateTimeOffset? expiry = null)
    {
        AuthenticationProperties properties = new() { ExpiresUtc = expiry ?? DateTimeOffset.UtcNow.AddHours(1) };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
        return new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "staff-1")], "Cookies")), properties, "Cookies");
    }
}
