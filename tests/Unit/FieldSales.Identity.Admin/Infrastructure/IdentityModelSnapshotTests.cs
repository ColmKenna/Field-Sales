using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using FieldSales.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

/// <summary>A model change without a migration fails here, before the SQL Server migration and adoption tests.
/// The migrations live in the Identity host's assembly, as configured in its Program.cs.</summary>
[Trait("Category", "Unit")]
public sealed class IdentityModelSnapshotTests
{
    private const string Unused = "Server=unused;Database=unused";
    private static readonly string MigrationsAssembly = typeof(ApplicationDbContext).Assembly.FullName!;

    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheIdentityModelIsBuilt()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(Unused, sql => sql.MigrationsAssembly(MigrationsAssembly)).Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheConfigurationStoreModelIsBuilt()
    {
        using ServiceProvider stores = StoreOptions(new ConfigurationStoreOptions());
        using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseApplicationServiceProvider(stores)
            .UseSqlServer(Unused, sql => sql.MigrationsAssembly(MigrationsAssembly)).Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Should_MatchTheMigrationSnapshot_When_TheOperationalStoreModelIsBuilt()
    {
        using ServiceProvider stores = StoreOptions(new OperationalStoreOptions());
        using var db = new PersistedGrantDbContext(new DbContextOptionsBuilder<PersistedGrantDbContext>()
            .UseApplicationServiceProvider(stores)
            .UseSqlServer(Unused, sql => sql.MigrationsAssembly(MigrationsAssembly)).Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static ServiceProvider StoreOptions<TOptions>(TOptions options) where TOptions : class =>
        new ServiceCollection().AddSingleton(options).BuildServiceProvider();
}
