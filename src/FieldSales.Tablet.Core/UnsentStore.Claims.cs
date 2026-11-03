using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed partial class UnsentStore
{
    // An upload's claim on an item (T-5.2.1): set before the item is sent, cleared by the answer that settles it.
    // Removing the work (Sent, or deleted after a refusal) removes the claim with it.
    private const string ClaimTable = """
        CREATE TABLE upload_claims(id TEXT PRIMARY KEY REFERENCES work(id) ON DELETE CASCADE, claimed_utc TEXT NOT NULL);
        """;

    /// <summary>Claims an item for upload, but only while it is still exactly <paramref name="queued"/> and unrefused. True
    /// when the item is claimed (a claim kept from an earlier unconfirmed Sync counts); false when it changed or went since
    /// the queue was read, so it must not be sent. The caller holds <see cref="OrderStore.GateFor"/>.</summary>
    public bool Claim(UnsentItem queued, DateTimeOffset claimedUtc)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO upload_claims(id, claimed_utc)
                SELECT id, $utc FROM work WHERE id=$id AND json=$json AND NOT EXISTS(SELECT 1 FROM work_faults WHERE id=$id)
                ON CONFLICT(id) DO NOTHING;
            SELECT EXISTS(SELECT 1 FROM work w JOIN upload_claims c ON c.id=w.id WHERE w.id=$id AND w.json=$json);
            """;
        command.Parameters.AddWithValue("$id", queued.Id.ToString());
        command.Parameters.AddWithValue("$json", queued.Json);
        command.Parameters.AddWithValue("$utc", claimedUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>Whether an upload has claimed this item and no answer has settled it yet. While it has, the server may hold
    /// this exact body, so the item must not change or be deleted. Upload bookkeeping, not part of the captured item.</summary>
    public bool IsClaimed(Guid id)
    {
        if (!File.Exists(workspace.UnsentPath)) return false;
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM upload_claims WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteScalar() is not null;
    }

    /// <summary>Clears a claim when the server provably never got the item (refused sign-in or permission, or no
    /// connection was made), so the rep can change it again.</summary>
    public void ReleaseClaim(Guid id)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM upload_claims WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

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
