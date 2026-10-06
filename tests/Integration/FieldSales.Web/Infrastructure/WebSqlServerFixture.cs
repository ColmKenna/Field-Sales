using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace FieldSales.Web.Tests;

public static class WebSqlServerFixture
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static MsSqlContainer? _container;

    public static async Task<string> CreateConnectionStringAsync(string? prefix = null)
    {
        if (_container == null)
        {
            await Lock.WaitAsync();
            try
            {
                if (_container == null)
                {
                    var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                    await container.StartAsync();
                    _container = container;
                }
            }
            finally
            {
                Lock.Release();
            }
        }

        var catalog = $"WebTest_{(prefix != null ? prefix + "_" : "")}{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = catalog
        };
        return builder.ConnectionString;
    }
}
