using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>"Sending 2 of 3": the item being sent and how many are going this Sync. <see cref="Finished"/> is reported
/// once the uploads are over, before the download.</summary>
public sealed record UploadProgress(int Sending, int Total)
{
    public static UploadProgress Finished { get; } = new(0, 0);
}

/// <summary>What the upload part of a Sync did: how many items were going, and how many the server confirmed.</summary>
public sealed record UploadSummary(int Total, int Sent)
{
    public static UploadSummary None { get; } = new(0, 0);
    public int NotSent => Total - Sent;
}

/// <summary>Why the upload stopped before the queue was finished, if it did.</summary>
public enum UploadStop { None, SignInRequired, NotPermitted, Unavailable }

/// <summary>Sends the rep's finished work, one item at a time, before the snapshot downloads (T-4.1.2).</summary>
/// <remarks>
/// Saved Calls go first, then Ready to Send Orders, oldest first in each. In Progress Orders, schedule changes and any
/// saved item that can't be read as finished work stay on the tablet untouched. An item is marked Sent only when the
/// server confirms it, and straight away, before the next item goes; if the confirmation is lost, the next Sync sends
/// the same item again and the server answers that it already has it. A refused item stays unsent and the next item
/// goes; a refused sign-in or permission stops everything; a lost connection or an unavailable server stops the
/// remaining uploads but leaves the download to be tried.
/// </remarks>
public sealed class WorkUploader(RepWorkspace workspace, HttpClient http, TimeProvider clock)
{
    /// <summary>The items this Sync would send, in the order they go.</summary>
    public static IReadOnlyList<UnsentItem> Queue(IEnumerable<UnsentItem> work) =>
    [
        .. work.Where(item => item.Kind == UnsentKind.Call).OrderBy(item => item.SavedUtc).ThenBy(item => item.Id),
        .. work.Where(item => item.Kind == UnsentKind.Order && IsReadyToSend(item)).OrderBy(item => item.SavedUtc).ThenBy(item => item.Id)
    ];

    public async Task<(UploadSummary Summary, UploadStop Stop)> UploadAsync(string token, SnapshotHooks hooks,
        IProgress<UploadProgress>? progress, CancellationToken ct)
    {
        UnsentStore unsent = new(workspace);
        if (!File.Exists(workspace.UnsentPath)) return (UploadSummary.None, UploadStop.None);
        IReadOnlyList<UnsentItem> queue = Queue(unsent.Read());
        int sent = 0;
        for (int index = 0; index < queue.Count; index++)
        {
            UnsentItem item = queue[index];
            progress?.Report(new(index + 1, queue.Count));
            (bool confirmed, UploadStop stop) = await SendAsync(item, token, ct);
            if (stop != UploadStop.None) return (new(queue.Count, sent), stop);
            if (!confirmed) continue;
            hooks.AtBoundary?.Invoke("upload-confirmed");
            unsent.MarkSent(item.Id, clock.GetUtcNow());
            sent++;
        }
        return (new(queue.Count, sent), UploadStop.None);
    }

    private async Task<(bool Confirmed, UploadStop Stop)> SendAsync(UnsentItem item, string token, CancellationToken ct)
    {
        string path = item.Kind == UnsentKind.Call ? TabletUploadContract.CallPath(item.Id) : TabletUploadContract.OrderPath(item.Id);
        using HttpRequestMessage request = new(HttpMethod.Put, path)
        {
            // Exactly what was saved, so a re-send is byte-for-byte the same item.
            Content = new StringContent(item.Json, Encoding.UTF8, TabletUploadContract.MediaType)
        };
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        // No route, refused or timed out: the connection is gone, so nothing more is sent this Sync.
        catch (Exception exception) when (!ct.IsCancellationRequested
            && exception is HttpRequestException { StatusCode: null } or TaskCanceledException or IOException)
        {
            return (false, UploadStop.Unavailable);
        }
        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK or HttpStatusCode.Created:
                    return await IsReceiptForAsync(response, item.Id, ct) ? (true, UploadStop.None) : (false, UploadStop.Unavailable);
                case HttpStatusCode.Unauthorized: return (false, UploadStop.SignInRequired);
                case HttpStatusCode.Forbidden: return (false, UploadStop.NotPermitted);
                // A technical fault in this item (MI-52): it stays unsent and the next item goes.
                case HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.RequestEntityTooLarge:
                    return (false, UploadStop.None);
                default: return (false, UploadStop.Unavailable);
            }
        }
    }

    // A confirmation counts only when it names this item; anything else is treated as no confirmation.
    private static async Task<bool> IsReceiptForAsync(HttpResponseMessage response, Guid id, CancellationToken ct)
    {
        try
        {
            UploadReceipt? receipt = await response.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt, ct);
            return receipt is { Status: UploadReceiptStatus.Received or UploadReceiptStatus.AlreadyReceived } && receipt.Id == id;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or IOException or HttpRequestException)
        {
            return false;
        }
    }

    private static bool IsReadyToSend(UnsentItem item)
    {
        try
        {
            using JsonDocument body = JsonDocument.Parse(item.Json);
            return body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty("state", out JsonElement state) && state.ValueKind == JsonValueKind.String
                && state.ValueEquals(nameof(OrderState.ReadyToSend));
        }
        catch (JsonException) { return false; }
    }
}
