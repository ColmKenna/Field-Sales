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

/// <summary>What the upload part of a Sync did: how many items were going, how many the server confirmed, and why it
/// stopped before the end of the queue, if it did.</summary>
public sealed record UploadSummary(int Total, int Sent, UploadStop Stop = UploadStop.None, int NeedsAttention = 0)
{
    public static UploadSummary None { get; } = new(0, 0);
    public int NotSent => Total - Sent;
    public int Waiting => NotSent - NeedsAttention;
}

/// <summary>Why the upload stopped before the queue was finished, if it did (T-4.1.3 tells the rep).</summary>
public enum UploadStop
{
    None,
    /// <summary>No usable sign-in: the tablet has no token, or the server refused it (401). Nothing more is sent.</summary>
    SignInRequired,
    /// <summary>The server refused the rep's permission (403). Nothing more is sent.</summary>
    NotPermitted,
    /// <summary>No answer came back: no route, a refused or dropped connection, or a timeout.</summary>
    ConnectionLost,
    /// <summary>The server answered with an error, or with a confirmation that doesn't name the item.</summary>
    ServerError
}

/// <summary>Sends the rep's finished work, one item at a time, before the snapshot downloads (T-4.1.2).</summary>
/// <remarks>
/// Saved Calls go first, then Ready to Send Orders, oldest first in each. In Progress Orders, schedule changes and any
/// saved item that can't be read as finished work stay on the tablet untouched. An item is marked Sent only when the
/// server confirms it, and straight away, before the next item goes; if the confirmation is lost, the next Sync sends
/// the same item again and the server answers that it already has it. A refused item stays unsent and the next item
/// goes; a refused sign-in or permission stops everything; a lost connection or a server error stops the remaining
/// uploads but leaves the download to be tried.
/// </remarks>
public sealed class WorkUploader(RepWorkspace workspace, HttpClient http, TimeProvider clock)
{
    /// <summary>The items this Sync would send, in the order they go.</summary>
    public static IReadOnlyList<UnsentItem> Queue(IEnumerable<UnsentItem> work) =>
    [
        .. work.Where(item => item.Rejection is null && item.Kind == UnsentKind.Call).OrderBy(item => item.SavedUtc).ThenBy(item => item.Id),
        .. work.Where(item => item.Rejection is null && item.Kind == UnsentKind.Order && IsReadyToSend(item)).OrderBy(item => item.SavedUtc).ThenBy(item => item.Id)
    ];

    /// <summary>How many items the next Sync would send, so the screen can say "Sending 1 of 3…" before it starts.</summary>
    public static int CountWaiting(RepWorkspace workspace) =>
        File.Exists(workspace.UnsentPath) ? Queue(new UnsentStore(workspace).Read()).Count : 0;

    public async Task<UploadSummary> UploadAsync(string token, SnapshotHooks hooks, IProgress<UploadProgress>? progress,
        CancellationToken ct)
    {
        UnsentStore unsent = new(workspace);
        if (!File.Exists(workspace.UnsentPath)) return UploadSummary.None;
        IReadOnlyList<UnsentItem> queue = Queue(unsent.Read());
        int sent = 0, attention = 0;
        for (int index = 0; index < queue.Count; index++)
        {
            UnsentItem item = queue[index];
            progress?.Report(new(index + 1, queue.Count));
            (bool confirmed, UploadStop stop) = await SendAsync(item, token, unsent, ct);
            if (stop != UploadStop.None) return new(queue.Count, sent, stop, attention);
            if (!confirmed) { attention++; continue; }
            hooks.AtBoundary?.Invoke("upload-confirmed");
            unsent.MarkSent(item.Id, clock.GetUtcNow());
            sent++;
        }
        return new(queue.Count, sent, NeedsAttention: attention);
    }

    private async Task<(bool Confirmed, UploadStop Stop)> SendAsync(UnsentItem item, string token, UnsentStore unsent, CancellationToken ct)
    {
        string path = item.Kind == UnsentKind.Call ? TabletUploadContract.CallPath(item.Id) : TabletUploadContract.OrderPath(item.Id);
        using HttpRequestMessage request = new(HttpMethod.Put, path)
        {
            // Exactly what was saved, so a re-send is byte-for-byte the same item.
            Content = new StringContent(item.Json, Encoding.UTF8, TabletUploadContract.MediaType)
        };
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        // No route, refused or timed out: the connection is gone, so nothing more is sent this Sync.
        catch (Exception exception) when (!ct.IsCancellationRequested
            && exception is HttpRequestException { StatusCode: null } or TaskCanceledException or IOException)
        {
            return (false, UploadStop.ConnectionLost);
        }
        using (response)
        {
            // ResponseHeadersRead avoids buffering arbitrary fault pages, but moves body reads outside
            // HttpClient's timeout. Keep those reads bounded in time as well as in size.
            using CancellationTokenSource bodyTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bodyTimeout.CancelAfter(http.Timeout);
            try
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK or HttpStatusCode.Created:
                        return await IsReceiptForAsync(response, item.Id, bodyTimeout.Token) ? (true, UploadStop.None) : (false, UploadStop.ServerError);
                    case HttpStatusCode.Unauthorized: return (false, UploadStop.SignInRequired);
                    case HttpStatusCode.Forbidden: return (false, UploadStop.NotPermitted);
                    // A technical fault in this item (MI-52): it stays unsent and the next item goes.
                    case HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.RequestEntityTooLarge:
                        UploadRejection rejection = await ReadRejectionAsync(response, bodyTimeout.Token);
                        unsent.MarkNeedsAttention(item, rejection);
                        return (false, UploadStop.None);
                    default: return (false, UploadStop.ServerError);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return (false, UploadStop.ConnectionLost); }
        }
    }

    // Bound support data even if a proxy sends an unexpectedly large error page. It never becomes rep-facing text.
    private async Task<UploadRejection> ReadRejectionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int maximum = 16 * 1024;
        string raw;
        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            byte[] buffer = new byte[maximum + 1];
            int length = 0, read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), ct)) > 0) length += read;
            raw = Encoding.UTF8.GetString(buffer, 0, Math.Min(length, maximum));
            if (length > maximum) raw += "\n[response truncated]";
        }
        catch (Exception error) when (error is IOException or HttpRequestException)
        {
            raw = "The server refused the item, but its reason could not be read.";
        }
        string? fault = null;
        try
        {
            using JsonDocument problem = JsonDocument.Parse(raw);
            if (problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty(UploadFaults.ProblemKey, out JsonElement code)
                && code.ValueKind == JsonValueKind.String) fault = code.GetString();
        }
        catch (JsonException) { /* An unreadable reason uses the contact-office sentence; retain the raw response. */ }
        return new(fault, raw, (int)response.StatusCode, clock.GetUtcNow());
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
