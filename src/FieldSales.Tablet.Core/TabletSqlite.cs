using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>The native SQLite engine this app was tested with (1C): 3.53.3 in WAL mode with synchronous FULL.</summary>
public sealed record SqliteEngine(string Version, string SourceId, string JournalMode, long Synchronous)
{
    public const string RequiredVersion = "3.53.3";
    private static readonly Lazy<bool> Initialized = new(() => { SQLitePCL.Batteries_V2.Init(); return true; });

    internal static SqliteConnection Open(string path)
    {
        _ = Initialized.Value;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Pooling = false, DefaultTimeout = 30
        }.ToString());
        connection.Open();
        Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA wal_autocheckpoint=1000;");
        return connection;
    }

    /// <summary>Fails when a different native build, journal mode or durability setting is in use.</summary>
    internal static SqliteEngine Preflight(SqliteConnection connection)
    {
        string Scalar(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
        }
        var engine = new SqliteEngine(Scalar("SELECT sqlite_version()"), Scalar("SELECT sqlite_source_id()"),
            Scalar("PRAGMA journal_mode"), long.Parse(Scalar("PRAGMA synchronous"), CultureInfo.InvariantCulture));
        if (engine.Version != RequiredVersion || engine.JournalMode != "wal" || engine.Synchronous != 2)
            throw new InvalidOperationException(
                $"Local storage check failed: SQLite {engine.Version}, {engine.JournalMode}, synchronous={engine.Synchronous}.");
        return engine;
    }

    internal static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Applies a store's schema once, and refuses a database written by a newer app.</summary>
    internal static void EnsureSchema(SqliteConnection connection, int version, string schema)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        long current = (long)command.ExecuteScalar()!;
        if (current == version) return;
        if (current != 0)
            throw new InvalidOperationException($"This tablet's local data uses format {current}; this app reads format {version}.");
        using var transaction = connection.BeginTransaction();
        Execute(connection, schema, transaction);
        Execute(connection, $"PRAGMA user_version={version}", transaction);
        transaction.Commit();
    }
}
