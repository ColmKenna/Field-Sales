using FieldSales.Api.Directory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FieldSales.Api.Tests;

public sealed class DirectoryModelTests
{
    [Fact]
    public void Should_MatchMigrationSnapshot_When_SourceFilesUseEitherLineEnding()
    {
        using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseSqlServer("Server=unused;Database=unused").Options);
        var constraints = db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .SelectMany(item => item.GetCheckConstraints()).ToArray();
        Assert.Contains(constraints, item => item.Name == "CK_Towns_Coordinates");
        Assert.All(constraints, item => Assert.False(item.Sql.Contains('\r'), item.Name));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
