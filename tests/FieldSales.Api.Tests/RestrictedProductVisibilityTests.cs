using FieldSales.Api.Catalogue;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class RestrictedProductVisibilityTests(RestrictedProductVisibilityTests.Sql sql)
    : IClassFixture<RestrictedProductVisibilityTests.Sql>
{
    [Fact]
    public async Task Should_HideProductFromEveryRep_When_ItsGroupIsArchived()
    {
        await using var db = sql.CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var other = await AddGroupAsync(db, "High-value equipment");
        Guid[] scope = [await AddProductAsync(db, group)];
        group.Archive();
        await db.SaveChangesAsync();
        Assert.Empty(await VisibleAsync(scope, group.Id)); // rep with the permission
        Assert.Empty(await VisibleAsync(scope, other.Id)); // rep without it
        Assert.Empty(await VisibleAsync(scope)); // new rep with no permissions
    }

    [Fact]
    public async Task Should_ShowProductOnlyToPermittedReps_When_ItsGroupIsActive()
    {
        await using var db = sql.CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var other = await AddGroupAsync(db, "High-value equipment");
        Guid restricted = await AddProductAsync(db, group);
        Guid otherRestricted = await AddProductAsync(db, other);
        Guid[] scope = [restricted, otherRestricted];
        Assert.Equal([restricted], await VisibleAsync(scope, group.Id));
        Assert.Equal([otherRestricted], await VisibleAsync(scope, other.Id));
        Assert.Equal(scope, await VisibleAsync(scope, group.Id, other.Id));
        Assert.Empty(await VisibleAsync(scope));
    }

    [Fact]
    public async Task Should_ShowProductToEveryRep_When_ItHasNoGroup()
    {
        await using var db = sql.CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        Guid unrestricted = await AddProductAsync(db, null);
        Guid[] scope = [unrestricted, await AddProductAsync(db, group)];
        group.Archive();
        await db.SaveChangesAsync();
        Assert.Equal([unrestricted], await VisibleAsync(scope, group.Id));
        Assert.Equal([unrestricted], await VisibleAsync(scope, Guid.NewGuid()));
        Assert.Equal([unrestricted], await VisibleAsync(scope));
    }

    [Fact]
    public async Task Should_RestorePreviousVisibility_When_GroupIsUnarchived()
    {
        await using var db = sql.CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var other = await AddGroupAsync(db, "High-value equipment");
        Guid restricted = await AddProductAsync(db, group);
        Guid[] scope = [restricted, await AddProductAsync(db, other), await AddProductAsync(db, null)];
        Guid[][] reps = [[group.Id], [other.Id], [group.Id, other.Id], []];
        List<Guid[]> before = [];
        foreach (Guid[] permitted in reps) before.Add(await VisibleAsync(scope, permitted));
        Assert.Contains(restricted, before[0]);
        group.Archive();
        await db.SaveChangesAsync();
        foreach (Guid[] permitted in reps) Assert.DoesNotContain(restricted, await VisibleAsync(scope, permitted));
        group.Unarchive();
        await db.SaveChangesAsync();
        for (int rep = 0; rep < reps.Length; rep++) Assert.Equal(before[rep], await VisibleAsync(scope, reps[rep]));
    }

    private static async Task<RestrictionGroup> AddGroupAsync(CatalogueDbContext db, string name)
    {
        var group = RestrictionGroup.Create($"{name} {Guid.NewGuid():N}");
        db.RestrictionGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    private static async Task<Guid> AddProductAsync(CatalogueDbContext db, RestrictionGroup? group)
    {
        var category = new CategoryTree([]).Add(Guid.NewGuid().ToString());
        var product = Product.Create(Guid.NewGuid().ToString(), "Controlled Pain Relief 30s", category.Id, 4.80m, new DateOnly(2026, 10, 1));
        product.SetRestrictionGroup(group);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    // Reads through a fresh context, as the snapshot builder would, and returns ids in scope order.
    private async Task<Guid[]> VisibleAsync(Guid[] scope, params Guid[] permitted)
    {
        await using var db = sql.CreateContext();
        Guid[] visible = await db.Products.Where(product => scope.Contains(product.Id))
            .VisibleToRep(db.RestrictionGroups, permitted).Select(product => product.Id).ToArrayAsync();
        return scope.Where(visible.Contains).ToArray();
    }

    public sealed class Sql : IAsyncLifetime
    {
        private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public CatalogueDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(_sql.GetConnectionString()).Options);

        public async Task InitializeAsync()
        {
            await _sql.StartAsync();
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }

        public Task DisposeAsync() => _sql.DisposeAsync().AsTask();
    }
}
