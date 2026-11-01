using FieldSales.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Web.Tests;

/// <summary>A model change without a migration fails here, before any test host creates the schema.</summary>
[Trait("Category", "Unit")]
public sealed class StaffWebModelSnapshotTests
{
    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheStaffWebModelIsBuilt()
    {
        using var db = new StaffWebDbContext(new DbContextOptionsBuilder<StaffWebDbContext>()
            .UseSqlServer("Server=unused;Database=unused").Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
