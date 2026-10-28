using Microsoft.Data.SqlClient;

namespace FieldSales.Api.Tests;

// Privileged cleanup for the snapshot fixture's exclusively owned, initially empty catalog.
// Real migration tests continue to create and migrate their own fresh databases.
internal sealed class SnapshotDatabaseReset(string connectionString)
{
    private string[] _tables = [];

    public async Task InitializeAsync()
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        if (!settings.InitialCatalog.StartsWith("ApiTest_TabletSnapshot_", StringComparison.Ordinal))
            throw new InvalidOperationException("Snapshot reset requires its exclusively owned test catalog.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class = 1 AND is_disabled = 1)
                OR EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1)
                OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_disabled = 1)
                THROW 50000, 'Snapshot reset requires enabled triggers and constraints.', 1;
            SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
            FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'__EFMigrationsHistory'
            ORDER BY object_id;
            """;
        List<string> tables = [];
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        _tables = tables.ToArray();

        // Do not silently discard reference data if future migrations introduce seed rows.
        command.CommandText = string.Join("\n", _tables.Select(table =>
            $"IF EXISTS (SELECT 1 FROM {table}) THROW 50000, 'Snapshot reset requires initially empty migrated tables.', 1;"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Main-contact cycles and append-only history require privileged cleanup. All
        // changes, including constraint/trigger state, roll back if any reset step fails.
        command.CommandText = string.Join("\n", _tables.Select(table =>
            $"DISABLE TRIGGER ALL ON {table}; ALTER TABLE {table} NOCHECK CONSTRAINT ALL;")) + "\n" +
            string.Join("\n", _tables.Select(table => $"DELETE FROM {table};")) + "\n" +
            string.Join("\n", _tables.Select(table =>
                $"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT ALL; ENABLE TRIGGER ALL ON {table};"));
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
