using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Tests;

// One migrated catalog per test class instead of one per test. Each case calls ResetAsync before and
// after it runs, so it starts from the same empty schema a fresh catalog would give it. Classes that
// test migrations themselves keep migrating their own fresh, separately named catalogs.

/// <summary>A Directory (and Coverage) catalog shared by one test class.</summary>
public sealed class DirectoryTestDatabase(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Owner = "Directory";
    private readonly string _catalogName = $"ApiTest_{Owner}_{Guid.NewGuid():N}";
    private OwnedCatalogReset? _reset;

    public DbContextOptions<DirectoryDbContext> Options(string? name = null) =>
        new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(ConnectionString(name)).Options;

    public DirectoryDbContext Create() => new(Options());

    public async Task InitializeAsync()
    {
        await using (var db = Create()) await db.Database.MigrateAsync();
        _reset = new OwnedCatalogReset(ConnectionString(), Owner);
        await _reset.InitializeAsync();
    }

    public Task ResetAsync() =>
        (_reset ?? throw new InvalidOperationException("The Directory catalog is not initialized.")).ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private string ConnectionString(string? name = null) =>
        new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = name ?? _catalogName }.ConnectionString;
}

/// <summary>A Catalogue catalog shared by one test class.</summary>
public sealed class CatalogueTestDatabase(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Owner = "Catalogue";
    private readonly string _connectionString = fixture.CreateConnectionString(Owner);
    private OwnedCatalogReset? _reset;

    public CatalogueDbContext Create() =>
        new(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(_connectionString).Options);

    public async Task InitializeAsync()
    {
        await using (var db = Create()) await db.Database.MigrateAsync();
        _reset = new OwnedCatalogReset(_connectionString, Owner);
        await _reset.InitializeAsync();
    }

    public Task ResetAsync() =>
        (_reset ?? throw new InvalidOperationException("The Catalogue catalog is not initialized.")).ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
