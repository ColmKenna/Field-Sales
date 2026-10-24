using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public enum SyncStatus { Completed, NoConnection, SignInRequired, NotPermitted, UpdateRequired, InsufficientSpace, Failed, Cancelled }

/// <summary>What a Sync did. Every outcome other than <see cref="SyncStatus.Completed"/> leaves data and work as they were.</summary>
public sealed record SyncOutcome(SyncStatus Status, string Message, ActiveSnapshot? Active)
{
    public bool Completed => Status == SyncStatus.Completed;
}

/// <summary>Rep-facing Sync text. T-4.1.3 owns the full set of failure causes.</summary>
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
}

/// <summary>Supplies the signed-in rep's current access token, or null when a connected sign-in is needed.</summary>
public interface IAccessTokenSource
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct);
}

/// <summary>Downloads the rep's Morning Snapshot and swaps it in only when complete; the previous one stays in use until then.</summary>
public sealed partial class TabletSync(RepWorkspace workspace, HttpClient http, IAccessTokenSource tokens, IStorageSpace space,
    TimeProvider? clock = null)
{
    /// <summary>1C: free space of twice the current snapshot database plus this reserve is required before Sync.</summary>
    public const long ReserveBytes = 512L * 1024 * 1024;
    internal const long MaximumGzipBytes = 512L * 1024 * 1024;

    public SnapshotStore Store { get; } = new(workspace, clock);

    public async Task<SyncOutcome> SyncAsync(SnapshotHooks? hooks = null, CancellationToken ct = default)
    {
        hooks ??= new();
        if (space.AvailableBytes(workspace.DirectoryPath) < 2 * workspace.SnapshotBytes() + ReserveBytes)
            return Outcome(SyncStatus.InsufficientSpace, SyncMessages.InsufficientSpace);
        try
        {
            string? token = await tokens.GetAccessTokenAsync(ct);
            if (token is null) return Outcome(SyncStatus.SignInRequired, SyncMessages.SignInRequired);
            (SyncOutcome? refused, ExpectedSnapshot? expected) = await DownloadAsync(token, hooks, ct);
            if (refused is not null) return refused;
            ActiveSnapshot active = await Store.ApplyAsync(workspace.TransferPath, expected!, hooks, ct);
            return new(SyncStatus.Completed, SyncMessages.Completed(active.CompletedUtc.ToLocalTime()), active);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Outcome(SyncStatus.Cancelled, SyncMessages.Cancelled);
        }
        // No route, no DNS, refused or timed out before a response: the tablet has no usable connection.
        catch (Exception exception) when (exception is HttpRequestException { StatusCode: null } or TaskCanceledException)
        {
            return Outcome(SyncStatus.NoConnection, SyncMessages.NoConnection);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or SqliteException or HttpRequestException)
        {
            return Outcome(SyncStatus.Failed, SyncMessages.Failed);
        }
        finally { File.Delete(workspace.TransferPath); }
    }

    /// <summary>Deletes copies retired at the last switch, or left by an interrupted import (D2-B).</summary>
    public Task CleanupAsync(SnapshotHooks? hooks = null, CancellationToken ct = default) => Store.PurgeRetiredAsync(hooks, ct);

    private SyncOutcome Outcome(SyncStatus status, string message) => new(status, message, Store.ReadActive());

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
