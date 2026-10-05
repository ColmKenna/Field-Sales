using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace FieldSales.Api.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string ConnectionString => _container.GetConnectionString();

    public string CreateConnectionString(string? prefix = null, string? catalogName = null)
    {
        var catalog = catalogName ?? $"ApiTest_{(prefix != null ? prefix + "_" : "")}{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = catalog
        };
        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Api SQL Server";
}
