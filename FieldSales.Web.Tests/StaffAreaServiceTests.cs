using System.Security.Claims;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

public sealed class StaffAreaServiceTests
{
    [Fact]
    public async Task RememberedAreaIsReturnedOnlyWhileStaffStillHasItsRole()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using ServiceProvider services = CreateServices(connection);
        await using StaffWebDbContext db = services.GetRequiredService<StaffWebDbContext>();
        StaffAreaService service = new(services, TimeProvider.System);
        ClaimsPrincipal user = User(StaffRoles.FieldSalesperson, StaffRoles.SalesManager);

        Assert.True(await service.RememberAreaAsync(user, StaffAreas.Manager));
        Assert.Equal("/Manager", await service.GetLastPermittedAreaAsync(user));
        Assert.Null(await service.GetLastPermittedAreaAsync(User(StaffRoles.FieldSalesperson)));
        Assert.Null(await service.GetLastPermittedAreaAsync(User(StaffRoles.HeadOfficeUser)));
    }

    [Fact]
    public async Task CannotRememberAnUnknownOrUnpermittedArea()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using ServiceProvider services = CreateServices(connection);
        await using StaffWebDbContext db = services.GetRequiredService<StaffWebDbContext>();
        StaffAreaService service = new(services, TimeProvider.System);

        Assert.False(await service.RememberAreaAsync(User(StaffRoles.FieldSalesperson), StaffAreas.Manager));
        Assert.False(await service.RememberAreaAsync(User(StaffRoles.FieldSalesperson), "unknown"));
        Assert.Empty(await db.StaffAreaPreferences.ToListAsync());
    }

    [Theory]
    [InlineData("/Rep", "rep")]
    [InlineData("/Manager/Reports?month=4", "manager")]
    [InlineData("/HeadOffice/#tools", "head-office")]
    [InlineData("//attacker.test/Rep", null)]
    [InlineData("https://attacker.test/Manager", null)]
    [InlineData("/Representative", null)]
    public void LocalPathMapsOnlyToItsStaffArea(string url, string? expected) =>
        Assert.Equal(expected, StaffAreas.ForLocalUrl(url)?.Key);

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static ServiceProvider CreateServices(SqliteConnection connection)
    {
        ServiceCollection services = new();
        services.AddDbContext<StaffWebDbContext>(options => options.UseSqlite(connection));
        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<StaffWebDbContext>().Database.EnsureCreated();
        return provider;
    }

    private static ClaimsPrincipal User(params string[] roles) => new(new ClaimsIdentity(
        [new Claim("sub", "staff-1"), .. roles.Select(role => new Claim("role", role))],
        "Cookies", "name", "role"));
}
