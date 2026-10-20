using Microsoft.Data.Sqlite;

namespace FieldSales.TabletSpike.Core;

public sealed record SqliteEnvironment(string Version, string SourceId, string JournalMode, long Synchronous,
    string[] CompileOptions, string NativePackage = "SQLitePCLRaw.lib.e_sqlite3/3.53.3",
    string ProviderPackage = "Microsoft.Data.Sqlite.Core/10.0.10")
{
    private static readonly Lazy<bool> Initialized = new(() => { SQLitePCL.Batteries_V2.Init(); return true; });
    public static SqliteConnection Open(string path)
    {
        _ = Initialized.Value;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Pooling = false, DefaultTimeout = 30
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA wal_autocheckpoint=1000;";
        command.ExecuteNonQuery();
        return connection;
    }

    public static SqliteEnvironment Inspect(SqliteConnection connection)
    {
        string Scalar(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
        }
        var options = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA compile_options";
            using var rows = command.ExecuteReader();
            while (rows.Read()) options.Add(rows.GetString(0));
        }
        var result = new SqliteEnvironment(Scalar("SELECT sqlite_version()"), Scalar("SELECT sqlite_source_id()"),
            Scalar("PRAGMA journal_mode"), long.Parse(Scalar("PRAGMA synchronous")), options.ToArray());
        if (result.Version != "3.53.3" || result.JournalMode != "wal" || result.Synchronous != 2 ||
            options.Contains("THREADSAFE=0"))
            throw new InvalidOperationException($"SQLite preflight failed: loaded {result.Version}, {result.JournalMode}, sync={result.Synchronous}.");
        return result;
    }
}
