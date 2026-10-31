using System.Globalization;
using FieldSales.Ordering.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed partial class UnsentStore
{
    // The payload stays byte-for-byte as captured. Removing work also removes its fault, atomically.
    private const string FaultTable = """
        CREATE TABLE work_faults(id TEXT PRIMARY KEY REFERENCES work(id) ON DELETE CASCADE,
            fault TEXT NULL, raw_reason TEXT NOT NULL, http_status INTEGER NOT NULL, rejected_utc TEXT NOT NULL);
        """;

    /// <summary>Records a technical refusal only if this is still the version that was uploaded.</summary>
    public bool MarkNeedsAttention(UnsentItem uploaded, UploadRejection rejection)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO work_faults(id, fault, raw_reason, http_status, rejected_utc)
            SELECT id, $fault, $raw, $status, $utc FROM work WHERE id=$id AND json=$json
            ON CONFLICT(id) DO UPDATE SET fault=excluded.fault, raw_reason=excluded.raw_reason,
                http_status=excluded.http_status, rejected_utc=excluded.rejected_utc;
            """;
        command.Parameters.AddWithValue("$id", uploaded.Id.ToString());
        command.Parameters.AddWithValue("$json", uploaded.Json);
        command.Parameters.AddWithValue("$fault", (object?)rejection.Fault ?? DBNull.Value);
        command.Parameters.AddWithValue("$raw", rejection.RawReason);
        command.Parameters.AddWithValue("$status", rejection.HttpStatus);
        command.Parameters.AddWithValue("$utc", rejection.RejectedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return command.ExecuteNonQuery() == 1;
    }

    /// <summary>After review, offers the same rejected Ready to Send Order at the next manual Sync.
    /// Calls have no re-mark workflow. Never changes the payload or creates a new identity.</summary>
    public bool MarkReviewedReady(Guid id)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        UnsentItem? item = Find(id);
        if (item is not { Kind: UnsentKind.Order, Rejection: not null }
            || OrderStore.Parse(item) is not { State: OrderState.ReadyToSend, Lines.Count: > 0 } order
            || order.Lines.Any(line => line is null || line.Price is null)) return false;
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM work_faults WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        bool changed = command.ExecuteNonQuery() == 1;
        transaction.Commit();
        return changed;
    }

    /// <summary>The only Call deletion operation. Confirmation and a current rejection are required;
    /// Sent items and other unsent work cannot be removed through this operation.</summary>
    public bool DeleteRejected(Guid id, bool confirmed)
    {
        if (!confirmed || !File.Exists(workspace.UnsentPath)) return false;
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM work WHERE id=$id AND EXISTS(SELECT 1 FROM work_faults WHERE id=$id)";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery() == 1;
    }
}
