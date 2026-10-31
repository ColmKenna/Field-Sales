using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public enum SyncStatus { Completed, NoConnection, SignInRequired, NotPermitted, UpdateRequired, InsufficientSpace, Failed, Cancelled }

/// <summary>What a Sync did. <see cref="Upload"/> says how many finished items were going, how many the server confirmed
/// and why the uploads stopped early, if they did; only confirmed items changed. Every outcome other than
/// <see cref="SyncStatus.Completed"/> leaves the snapshot as it was.</summary>
public sealed record SyncOutcome(SyncStatus Status, string Message, ActiveSnapshot? Active)
{
    public bool Completed => Status == SyncStatus.Completed;
    public UploadSummary Upload { get; init; } = UploadSummary.None;
}

/// <summary>What the rep reads after a Sync that didn't simply finish: a sentence about their finished work, then one about
/// their day. Either may be absent.</summary>
public sealed record SyncReport(string? Work, string? Day);

/// <summary>Rep-facing Sync text, including why a Sync didn't complete (T-4.1.3).</summary>
public static class SyncMessages
{
    public const string NoConnection = "No connection — your work is saved on this tablet";
    public const string UpdateRequired = SnapshotContract.UpdateRequiredTitle;
    public const string SignInRequired = "Sign in again to sync.";
    public const string NotPermitted = "This app is for Field Salespeople.";
    public const string InsufficientSpace = "There isn't enough free space on this tablet to sync. Your data and saved work are unchanged.";
    public const string Failed = "Sync didn't finish. Your data and saved work on this tablet are unchanged.";
    public const string Cancelled = "Sync was cancelled. Your data and saved work on this tablet are unchanged.";
    public static string Completed(DateTimeOffset localCompleted) => $"Last synced {localCompleted:HH:mm}";

    /// <summary>Said when some finished items didn't go and no cause below applies.</summary>
    public static string NotSent(int count) => count == 1
        ? "1 item not sent. It'll send next Sync."
        : $"{count} items not sent. They'll send next Sync.";

    /// <summary>US-001 S2: the connection went during the uploads after something got through.</summary>
    public static string PoorConnection(int count) => count == 1
        ? "1 item not sent — poor connection. It'll send next Sync."
        : $"{count} items not sent — poor connection. They'll send next Sync.";

    /// <summary>What happened to the rep's finished work and to their day, in the rep's words (WI-040 D1–D6).</summary>
    public static SyncReport Explain(SyncOutcome outcome)
    {
        UploadSummary upload = outcome.Upload;
        // The connection failed during the uploads, but an item was confirmed or the day downloaded afterwards.
        bool poorConnection = upload is { Stop: UploadStop.ConnectionLost, NotSent: > 0 } && (upload.Sent > 0 || outcome.Completed);
        string? work = upload.NotSent == 0 ? null : poorConnection ? PoorConnection(upload.NotSent) : NotSent(upload.NotSent);
        // A new day shows in "Last synced"; and one sentence about the connection is enough.
        string? day = outcome.Completed || (poorConnection && outcome.Status == SyncStatus.NoConnection) ? null : outcome.Message;
        return new(work, day);
    }

    /// <summary>Home's "Last synced 07:42"; a sync from an earlier day also shows its date.</summary>
    public static string LastSynced(DateTimeOffset completedUtc, TimeProvider clock)
    {
        DateTimeOffset completed = TimeZoneInfo.ConvertTime(completedUtc, clock.LocalTimeZone);
        DateTimeOffset now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone);
        return completed.Date == now.Date ? Completed(completed)
            : $"Last synced {completed.ToString("d MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}

/// <summary>Supplies the signed-in rep's current access token, or null when a connected sign-in is needed.</summary>
public interface IAccessTokenSource
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct);
}

/// <summary>Sends the rep's finished work first (T-4.1.2), then downloads the Morning Snapshot and swaps it in only when
/// complete; the previous one stays in use until then. The download is tried whatever happened to the uploads, unless
/// the sign-in or permission was refused.</summary>
public sealed partial class TabletSync(RepWorkspace workspace, HttpClient http, IAccessTokenSource tokens, IStorageSpace space,
    TimeProvider? clock = null)
{
    /// <summary>1C: free space of twice the current snapshot database plus this reserve is required before Sync.</summary>
    public const long ReserveBytes = 512L * 1024 * 1024;
    internal const long MaximumGzipBytes = 512L * 1024 * 1024;

    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public SnapshotStore Store { get; } = new(workspace, clock);

    public async Task<SyncOutcome> SyncAsync(SnapshotHooks? hooks = null, CancellationToken ct = default,
        IProgress<UploadProgress>? progress = null)
    {
        hooks ??= new();
        UploadSummary upload = UploadSummary.None;
        try
        {
            string? token = await tokens.GetAccessTokenAsync(ct);
            // Nothing is sent without a sign-in, but the rep is told how many finished items are waiting for one.
            if (token is null)
                return Outcome(SyncStatus.SignInRequired, SyncMessages.SignInRequired, new(CountWaiting(), 0, UploadStop.SignInRequired));
            upload = await new WorkUploader(workspace, http, time).UploadAsync(token, hooks, progress, ct);
            if (upload.Total > 0) progress?.Report(UploadProgress.Finished);
            if (upload.Stop == UploadStop.SignInRequired) return Outcome(SyncStatus.SignInRequired, SyncMessages.SignInRequired, upload);
            if (upload.Stop == UploadStop.NotPermitted) return Outcome(SyncStatus.NotPermitted, SyncMessages.NotPermitted, upload);
            // Uploads need no room for a snapshot; only the download does.
            if (space.AvailableBytes(workspace.DirectoryPath) < 2 * workspace.SnapshotBytes() + ReserveBytes)
                return Outcome(SyncStatus.InsufficientSpace, SyncMessages.InsufficientSpace, upload);
            (SyncOutcome? refused, ExpectedSnapshot? expected) = await DownloadAsync(token, hooks, ct);
            if (refused is not null) return refused with { Upload = upload };
            ActiveSnapshot active = await Store.ApplyAsync(workspace.TransferPath, expected!, hooks, ct);
            // "Last synced" is shown in the tablet's own time zone.
            return new(SyncStatus.Completed, SyncMessages.Completed(TimeZoneInfo.ConvertTime(active.CompletedUtc, time.LocalTimeZone)), active)
                { Upload = upload };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Outcome(SyncStatus.Cancelled, SyncMessages.Cancelled, upload);
        }
        // No route, no DNS, refused or timed out before a response: the tablet has no usable connection.
        catch (Exception exception) when (exception is HttpRequestException { StatusCode: null } or TaskCanceledException)
        {
            return Outcome(SyncStatus.NoConnection, SyncMessages.NoConnection, upload);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or SqliteException or HttpRequestException)
        {
            return Outcome(SyncStatus.Failed, SyncMessages.Failed, upload);
        }
        finally { File.Delete(workspace.TransferPath); }
    }

    /// <summary>Deletes copies retired at the last switch, or left by an interrupted import (D2-B).</summary>
    public Task CleanupAsync(SnapshotHooks? hooks = null, CancellationToken ct = default) => Store.PurgeRetiredAsync(hooks, ct);

    private SyncOutcome Outcome(SyncStatus status, string message, UploadSummary? upload = null) =>
        new(status, message, Store.ReadActive()) { Upload = upload ?? UploadSummary.None };

    // The count is a courtesy: an Unsent database that can't be read must not hide that a sign-in is needed.
    private int CountWaiting()
    {
        try { return WorkUploader.CountWaiting(workspace); }
        catch (Exception exception) when (exception is InvalidDataException or IOException or SqliteException) { return 0; }
    }

    private async Task<(SyncOutcome?, ExpectedSnapshot?)> DownloadAsync(string token, SnapshotHooks hooks, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, SnapshotContract.Path);
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Add(SnapshotContract.AcceptedMajorsHeader, string.Join(",", SnapshotStore.ReadableMajors));
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized: return (Outcome(SyncStatus.SignInRequired, SyncMessages.SignInRequired), null);
            case HttpStatusCode.Forbidden: return (Outcome(SyncStatus.NotPermitted, SyncMessages.NotPermitted), null);
            case HttpStatusCode.NotAcceptable: return (Outcome(SyncStatus.UpdateRequired, SyncMessages.UpdateRequired), null);
        }
        if (!response.IsSuccessStatusCode) return (Outcome(SyncStatus.Failed, SyncMessages.Failed), null);
        string version = Header(response, SnapshotContract.VersionHeader);
        if (!int.TryParse(version.Split('.')[0], out int major) || !SnapshotStore.ReadableMajors.Contains(major))
            return (Outcome(SyncStatus.UpdateRequired, SyncMessages.UpdateRequired), null);
        ExpectedSnapshot expected = new(Hash(response, SnapshotContract.GzipSha256Header), Hash(response, SnapshotContract.PlainSha256Header));
        if (response.Content.Headers.ContentEncoding.Count != 0)
            throw new InvalidDataException("The snapshot must arrive as raw gzip bytes.");
        long? declared = response.Content.Headers.ContentLength;
        if (declared > MaximumGzipBytes) throw new InvalidDataException("The snapshot is larger than this app accepts.");

        await using Stream source = await response.Content.ReadAsStreamAsync(ct);
        await using FileStream file = new(workspace.TransferPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        while (await source.ReadAsync(buffer, ct) is int count and > 0)
        {
            total = checked(total + count);
            if (total > (declared ?? MaximumGzipBytes)) throw new InvalidDataException("The snapshot is longer than declared.");
            await file.WriteAsync(buffer.AsMemory(0, count), ct);
            hash.AppendData(buffer.AsSpan(0, count));
            if (total == count) { await file.FlushAsync(ct); hooks.AtBoundary?.Invoke("download"); }
        }
        await file.FlushAsync(ct);
        if ((declared is long length && total != length) || Convert.ToHexStringLower(hash.GetHashAndReset()) != expected.GzipSha256)
            throw new InvalidDataException("The snapshot download was incomplete.");
        return (null, expected);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) && values.SingleOrDefault() is { } value
            ? value : throw new InvalidDataException($"The snapshot response has no {name} header.");

    private static string Hash(HttpResponseMessage response, string name) =>
        Sha256Hex().IsMatch(Header(response, name)) ? Header(response, name) : throw new InvalidDataException($"Invalid {name} header.");

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex();
}
