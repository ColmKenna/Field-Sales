using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace FieldSales.Web.Tests;

public abstract class WebSqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public Task InitializeAsync() => FixtureCleanup.OnFailureAsync(async () =>
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
    }, DisposeAsync);

    public string CreateConnectionString(string prefix)
    {
        var container = _container ?? throw new InvalidOperationException("The SQL fixture is not initialized.");
        return new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = $"WebTest_{prefix}_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        var container = Interlocked.Exchange(ref _container, null);
        if (container is not null) await container.DisposeAsync();
    }
}

public sealed class CatalogueSqlServerFixture : WebSqlServerFixture { }
public sealed class DirectorySqlServerFixture : WebSqlServerFixture { }

[CollectionDefinition(Name)]
public sealed class CatalogueSqlServerCollection : ICollectionFixture<CatalogueSqlServerFixture>
{
    public const string Name = "Web Catalogue SQL Server";
}

[CollectionDefinition(Name)]
public sealed class DirectorySqlServerCollection : ICollectionFixture<DirectorySqlServerFixture>
{
    public const string Name = "Web Directory SQL Server";
}
