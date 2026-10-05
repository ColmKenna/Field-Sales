using System.Security.Claims;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

[Trait("Category", "Unit")]

public sealed class StaffAreaServiceTests
{
    [Theory]
    [InlineData(0, false, StaffLandingStatus.Denied, null)]
    [InlineData(1, false, StaffLandingStatus.Direct, "/Rep")]
    [InlineData(2, false, StaffLandingStatus.Choose, null)]
    [InlineData(2, true, StaffLandingStatus.Direct, "/Manager")]
    public async Task Landing_UsesRememberedAreaBeforeSingleOrMultipleChoices(int count, bool remember, StaffLandingStatus expected, string? route)
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using ServiceProvider services = CreateServices(connection);
        StaffAreaService service = new(services, TimeProvider.System);
        string[] roles = new[] { StaffRoles.FieldSalesperson, StaffRoles.SalesManager }.Take(count).ToArray();
        ClaimsPrincipal user = User(roles);
        if (remember) Assert.True(await service.RememberAreaAsync(user, StaffAreas.Manager));
        StaffLanding landing = await service.ResolveLandingAsync(user);
        Assert.Equal(expected, landing.Status);
        Assert.Equal(route, landing.Route);
        Assert.Equal(count, landing.Areas.Count);
        if (remember)
        {
            StaffLanding revoked = await service.ResolveLandingAsync(User(StaffRoles.FieldSalesperson));
            Assert.Equal("/Rep", revoked.Route);
        }
    }

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
    [InlineData("/HeadOffice/Categories", "head-office")]
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
