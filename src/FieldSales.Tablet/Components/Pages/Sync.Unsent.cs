using FieldSales.Tablet.Core;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Components.Pages;

public partial class Sync
{
    private IReadOnlyList<UnsentView>? items;
    private UnsentView? reviewing;
    private UnsentView? deleting;
    private bool changing;
    private string? actionMessage;
    private string? listProblem;
    private long unsentVersion;

    private static string SectionName(UnsentSection section) => section switch
    {
        UnsentSection.NeedsAttention => "Needs Attention",
        UnsentSection.ReadyToSend => "Ready to Send",
        UnsentSection.InProgress => "In Progress",
        _ => "Other saved work"
    };

    private void ReadItems()
    {
        items = null; reviewing = null; deleting = null; listProblem = null;
        if (rep is null) return;
        unsentVersion = Tablet.SessionVersion;
        try { items = Tablet.ReadUnsentViews(rep); }
        catch (Exception error) when (error is IOException or InvalidDataException or SqliteException or System.Text.Json.JsonException)
        {
            listProblem = "Your unsent items couldn't be opened. Your work is still saved on this tablet.";
        }
    }

    private void OpenReview(UnsentView view)
    {
        if (busy || changing) return;
        reviewing = view;
        deleting = null;
        actionMessage = null;
    }

    private async Task ChangeWorkAsync(Guid id, bool delete)
    {
        if (busy || changing || disposed || rep is null) return;
        // Buttons carry no authority: the app checks the current rep/session and the store checks the current rejection.
        string capturedRep = rep;
        long version = unsentVersion;
        if (version != Tablet.SessionVersion) return;
        changing = true; actionMessage = null;
        try
        {
            bool changed = await Tablet.ChangeRejectedWorkAsync(capturedRep, id, version, delete, confirmed: delete);
            if (disposed || version != Tablet.SessionVersion) return;
            ReadItems();
            unsent = ReadUnsent(capturedRep);
            actionMessage = changed ? (delete ? "Item deleted." : "Ready to Send. It goes at your next Sync.")
                : "This item couldn't be changed. Open the list again to check it.";
            // A past Sync result no longer describes the queue after a rep action.
            Tablet.SyncNotices.Keep(capturedRep, version, null);
            ShowNotice();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
        {
            if (!disposed && version == Tablet.SessionVersion)
                actionMessage = "The change couldn't be saved. Check the item before trying again.";
        }
        finally { changing = false; }
    }
}
