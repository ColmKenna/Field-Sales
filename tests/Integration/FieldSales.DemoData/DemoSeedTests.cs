using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.DemoData;
using FieldSales.Identity.Data;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace FieldSales.DemoData.Tests;

public sealed class DemoSeedTests(DemoSqlServer server) : IClassFixture<DemoSqlServer>
{
    [Fact]
    public async Task Should_SeedConnectedScenarioOnce_AndPreserveEditsOnRepeat()
    {
        await using var catalogue = server.Catalogue();
        await using var directory = server.Directory();
        await using var provider = IdentityServices(server.Connection("Identity"));
        await using var scope = provider.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await identity.Database.MigrateAsync();
        await catalogue.Database.MigrateAsync();
        await directory.Database.MigrateAsync();
        Assert.True(await DemoStaffSeeder.SeedAsync(identity, users, roles, "DemoTestPass123!"));
        Assert.True(await DemoScenarioSeeder.SeedCatalogueAsync(catalogue));
        Assert.True(await DemoScenarioSeeder.SeedDirectoryAsync(directory));
        Assert.Equal(4, await identity.Users.CountAsync());
        Assert.Equal(4, await catalogue.Products.CountAsync());
        Assert.Equal(3, await directory.Customers.CountAsync());
        Assert.Equal(4, await directory.Locations.CountAsync());
        Assert.Equal(3, await directory.Contacts.CountAsync());
        Assert.Equal(3, await directory.TerritoryAssignments.CountAsync());
        Assert.Equal(3, await directory.AssignmentHistory.CountAsync());
        Assert.Equal(4, await directory.LocationContacts.CountAsync());
        Assert.Equal(3, await directory.Locations.CountAsync(location => location.MainContactId != null));
        foreach (var link in await directory.LocationContacts.Include(link => link.Contact).Include(link => link.Location).ToArrayAsync())
            Assert.Equal(FieldSales.Directory.Contracts.ContactStatus.Active, link.Contact.Status);
        var reporting = await directory.RepReportingLines.ToArrayAsync();
        Assert.Equal(2, reporting.Length);
        foreach (var line in reporting)
        {
            Assert.NotNull(await users.FindByIdAsync(line.RepSubject));
            Assert.NotNull(await users.FindByIdAsync(line.ManagerSubject));
        }
        Assert.True(await directory.Locations.AnyAsync(location => location.Name.Contains("Unassigned")));
        Guid creamId = (await catalogue.Products.SingleAsync(product => product.Code == "DEMO-001")).Id;
        Assert.Equal(3, await catalogue.ProductBasePrices.CountAsync(price => price.ProductId == creamId));

        Guid[] products = await catalogue.Products.OrderBy(product => product.Code).Select(product => product.Id).ToArrayAsync();
        var brand = await catalogue.Brands.SingleAsync(item => item.Name == "Demo Wicklow Care");
        brand.Rename("Brand edited during testing");
        catalogue.Products.Add(Product.Create("MANUAL-001", "Manual test product", (await catalogue.Categories.FirstAsync()).Id,
            1m, new(2026, 1, 1)));
        await catalogue.SaveChangesAsync();
        var aoife = (await users.FindByIdAsync(DemoStaffSeeder.AoifeSubject))!;
        Assert.True((await users.RemoveFromRoleAsync(aoife, BusinessRoles.FieldSalesperson)).Succeeded);
        string? passwordHash = aoife.PasswordHash;
        catalogue.ChangeTracker.Clear();
        directory.ChangeTracker.Clear();
        identity.ChangeTracker.Clear();
        // A different configured password on a repeat must not reset accounts or roles.
        Assert.False(await DemoStaffSeeder.SeedAsync(identity, users, roles, "DifferentPass123!"));
        Assert.False(await DemoScenarioSeeder.SeedCatalogueAsync(catalogue));
        Assert.False(await DemoScenarioSeeder.SeedDirectoryAsync(directory));
        Assert.Equal(5, await catalogue.Products.CountAsync());
        Assert.Equal(products, await catalogue.Products.Where(product => product.Code.StartsWith("DEMO-"))
            .OrderBy(product => product.Code).Select(product => product.Id).ToArrayAsync());
        Assert.Equal("Brand edited during testing", (await catalogue.Brands.SingleAsync(item => item.Id == brand.Id)).Name);
        aoife = (await users.FindByIdAsync(DemoStaffSeeder.AoifeSubject))!;
        Assert.Equal(passwordHash, aoife.PasswordHash);
        Assert.False(await users.IsInRoleAsync(aoife, BusinessRoles.FieldSalesperson));
        Assert.Equal(3, await directory.AssignmentHistory.CountAsync());
    }

    [Fact]
    public async Task Should_RollBackFailedBatch_AndAllowRetry()
    {
        await using var db = server.Catalogue();
        await db.Database.MigrateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeedBatch.RunAsync(db, async () =>
        {
            db.Brands.Add(Brand.Create("Incomplete sample"));
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Simulated failure");
        }));
        db.ChangeTracker.Clear();
        Assert.False(await db.Brands.AnyAsync());
        Assert.False(await DemoSeedBatch.IsSeededAsync(db));
        Assert.True(await DemoScenarioSeeder.SeedCatalogueAsync(db));
        Assert.Equal(4, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Should_SerializeConcurrentSeeds_WithoutDuplicates()
    {
        await using var first = server.Catalogue();
        await first.Database.MigrateAsync();
        await using var second = new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(first.Database.GetConnectionString()).Options);
        bool[] results = await Task.WhenAll(DemoScenarioSeeder.SeedCatalogueAsync(first), DemoScenarioSeeder.SeedCatalogueAsync(second));
        Assert.Single(results, created => created);
        Assert.Equal(4, await first.Products.CountAsync());
    }

    private static ServiceProvider IdentityServices(string connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }
}

public sealed class DemoSqlServer : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public Task InitializeAsync() => container.StartAsync();
    public Task DisposeAsync() => container.DisposeAsync().AsTask();
    public string Connection(string name) => new SqlConnectionStringBuilder(container.GetConnectionString())
        { InitialCatalog = "DemoSeedTest_" + name + "_" + Guid.NewGuid().ToString("N") }.ConnectionString;
    public CatalogueDbContext Catalogue() => new(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(Connection("Catalogue")).Options);
    public DirectoryDbContext Directory() => new(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(Connection("Directory")).Options);
}
