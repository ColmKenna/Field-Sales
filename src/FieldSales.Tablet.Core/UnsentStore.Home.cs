using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

internal sealed record HomeStoredWork(UnsentItem Item, DateTimeOffset? SentUtc, bool Claimed);

public sealed partial class UnsentStore
{
    // One SQLite statement sees work on exactly one side of the atomic move to Sent.
    // This is a read of the existing format, not another table or stored day record.
    internal IReadOnlyList<HomeStoredWork> ReadHomeWork()
    {
        if (!File.Exists(workspace.UnsentPath)) return [];
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT w.id, w.kind, w.saved_utc, w.json, f.fault, f.raw_reason, f.http_status, f.rejected_utc,
                   NULL AS sent_utc, c.id IS NOT NULL AS claimed
            FROM work w LEFT JOIN work_faults f ON f.id=w.id LEFT JOIN upload_claims c ON c.id=w.id
            UNION ALL
            SELECT s.id, s.kind, s.saved_utc, s.json, NULL, NULL, NULL, NULL, s.sent_utc, 0 FROM sent s
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<HomeStoredWork> result = [];
        while (reader.Read()) result.Add(new(ReadItem(reader), reader.IsDBNull(8) ? null
            : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture), reader.GetBoolean(9)));
        return result;
    }
}
