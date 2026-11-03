using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed partial class UnsentStore
{
    // An upload's claim on an item (T-5.2.1): set before the item is sent, cleared by the answer that settles it.
    // Removing the work (Sent, or deleted after a refusal) removes the claim with it.
    private const string ClaimTable = """
        CREATE TABLE upload_claims(id TEXT PRIMARY KEY REFERENCES work(id) ON DELETE CASCADE, claimed_utc TEXT NOT NULL);
        """;

    /// <summary>Deletes an unsent item the rep chose to delete, but only while it is exactly <paramref name="current"/>,
    /// has no technical refusal and no upload claim: an item the server may hold is never deleted. False when nothing
    /// was deleted.</summary>
    public bool DeleteUnclaimed(UnsentItem current)
    {
        if (!File.Exists(workspace.UnsentPath)) return false;
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM work WHERE id=$id AND json=$json
                AND NOT EXISTS(SELECT 1 FROM work_faults WHERE id=$id) AND NOT EXISTS(SELECT 1 FROM upload_claims WHERE id=$id)
            """;
        command.Parameters.AddWithValue("$id", current.Id.ToString());
        command.Parameters.AddWithValue("$json", current.Json);
        return command.ExecuteNonQuery() == 1;
    }
}
