using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Tests;

/// <summary>A model change without a migration fails here, before any SQL Server test applies the migrations.
/// The Directory model has the same check in <see cref="DirectoryModelTests"/>.</summary>
[Trait("Category", "Unit")]
public sealed class ModelSnapshotTests
{
    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheCatalogueModelIsBuilt()
    {
        using var db = new CatalogueDbContext(Options<CatalogueDbContext>());

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheOrderingModelIsBuilt()
    {
        using var db = new OrderingDbContext(Options<OrderingDbContext>());

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static DbContextOptions<TContext> Options<TContext>() where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlServer("Server=unused;Database=unused").Options;
}
