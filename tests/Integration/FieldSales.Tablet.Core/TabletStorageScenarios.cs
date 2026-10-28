using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

// WI-032 increment 2: the tablet's local storage, contract v1 (approved scenarios 9-13, and 7 and 14 on the tablet).
// The spike's verified behaviours each have a counterpart here: whole-generation activation, the separate Unsent
// store, recovery after a killed process, rejection of bad payloads and the pinned native engine.
public sealed class TabletStorageScenarios : IDisposable
{
    private readonly TestTablet _tablet = new();

    public void Dispose() => _tablet.Dispose();

    [Fact]
    public async Task Should_ReplaceTheSnapshotInOnePiece_When_DownloadCompletes()
    {
        TabletSnapshot morning = Snapshots.Build("colm", locations: 3, products: 200, label: "A");
        ActiveSnapshot first = await _tablet.SyncAsync(morning);
        Assert.Equal(morning.Manifest.Counts, first.Manifest.Counts);
        Assert.Equal(_tablet.Clock.GetUtcNow(), first.CompletedUtc);
        Assert.Equal(["A shop 0000", "A shop 0001", "A shop 0002"], _tablet.Sync().Store.ReadLocations().Select(row => row.Name));

        _tablet.Clock.Advance(TimeSpan.FromHours(8));
        TabletSnapshot evening = Snapshots.Build("colm", locations: 2, products: 150, label: "B");
        _tablet.Server.Serve(EncodedSnapshot.From(evening));
        SyncOutcome outcome = await _tablet.Sync().SyncAsync();
        Assert.Equal(SyncStatus.Completed, outcome.Status);
        Assert.Equal($"Last synced {TimeZoneInfo.ConvertTime(_tablet.Clock.GetUtcNow(), _tablet.Clock.LocalTimeZone):HH:mm}", outcome.Message);
        // Never a mix: Home reads only the new generation, and the previous one is kept whole until cleanup.
        SnapshotStore store = _tablet.Sync().Store;
        Assert.Equal(outcome.Active!.Generation, store.ReadActive()!.Generation);
        Assert.Equal(["B shop 0000", "B shop 0001"], store.ReadLocations().Select(row => row.Name));
        Assert.Equal([("complete", RowCount(morning)), ("complete", RowCount(evening))],
            store.Generations().Select(row => (row.State, row.Rows)));

        // A third snapshot retires the oldest copy; cleanup then deletes it in batches.
        await _tablet.SyncAsync(Snapshots.Build("colm", locations: 1, products: 10, label: "C"));
        Assert.Equal(["retired", "complete", "complete"], store.Generations().Select(row => row.State));
        await _tablet.Sync().CleanupAsync();
        Assert.Equal(["complete", "complete"], store.Generations().Select(row => row.State));
    }

    public static TheoryData<string> Interruptions => ["connection-drop", "short-body", "gzip-hash", "plain-hash", "counts",
        "other-rep", "corrupt-gzip", "truncated-json", "unreadable-version", "server-error"];

    [Theory]
    [MemberData(nameof(Interruptions))]
    public async Task Should_KeepTheOldSnapshotAndUnsentWork_When_DownloadIsInterrupted(string interruption)
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 4, products: 300));
        UnsentStore work = _tablet.SaveWork();
        string fingerprint = work.Fingerprint();
        TabletSnapshot next = Snapshots.Build("colm", locations: 2, products: 400, label: "N");
        EncodedSnapshot encoded = EncodedSnapshot.From(next);
        switch (interruption)
        {
            case "connection-drop": _tablet.Server.Serve(encoded, bytes => FailingContent.Create(bytes, bytes.Length / 2, throwAtEnd: true)); break;
            case "short-body": _tablet.Server.Serve(encoded, bytes => FailingContent.Create(bytes, bytes.Length / 2, throwAtEnd: false)); break;
            case "gzip-hash": _tablet.Server.Serve(encoded, gzipHash: new string('0', 64)); break;
            case "plain-hash": _tablet.Server.Serve(encoded, plainHash: new string('0', 64)); break;
            case "counts":
                _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 2, 400, label: "N", counts: new(2, 2, 401, 800)))); break;
            case "other-rep": _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("aoife", 2, 400, label: "N"))); break;
            case "corrupt-gzip":
                byte[] corrupt = [.. encoded.Gzip];
                for (int index = corrupt.Length / 3; index < corrupt.Length / 3 + 64; index++) corrupt[index] ^= 0x5A;
                _tablet.Server.Serve(EncodedSnapshot.FromJson([]) with { Gzip = corrupt, GzipSha256 = Hash(corrupt) }); break;
            case "truncated-json":
                byte[] plain = JsonSerializer.SerializeToUtf8Bytes(next, SnapshotContract.JsonOptions);
                _tablet.Server.Serve(EncodedSnapshot.FromJson(plain[..(plain.Length * 2 / 3)])); break;
            case "unreadable-version": _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 2, 400, label: "N", version: "2.0"))); break;
            case "server-error": _tablet.Server.Respond = _ => new(HttpStatusCode.InternalServerError); break;
        }

        SyncOutcome outcome = await _tablet.Sync().SyncAsync();
        Assert.Equal(interruption == "unreadable-version" ? SyncStatus.UpdateRequired : SyncStatus.Failed, outcome.Status);
        AssertUnchanged(old, fingerprint);
        Assert.False(File.Exists(_tablet.Workspace().TransferPath));
        // Reopening and cleanup leave only the old snapshot; a clean Sync then works normally.
        await _tablet.Sync().CleanupAsync();
        Assert.Equal([(old.Generation, "complete")], _tablet.Sync().Store.Generations().Select(row => (row.Generation, row.State)));
        Assert.Equal(next.Manifest.Counts, (await _tablet.SyncAsync(next)).Manifest.Counts);
        Assert.Equal(fingerprint, work.Fingerprint());
    }

    [Theory]
    [InlineData("download")]
    [InlineData("import")]
    [InlineData("before-activation")]
    public async Task Should_KeepTheOldSnapshotAndUnsentWork_When_SyncIsCancelled(string boundary)
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 4, products: 300));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", locations: 2, products: 400, label: "N")));
        using CancellationTokenSource cancel = new();
        SyncOutcome outcome = await _tablet.Sync().SyncAsync(new() { AtBoundary = reached => { if (reached == boundary) cancel.Cancel(); } },
            cancel.Token);
        Assert.Equal((SyncStatus.Cancelled, SyncMessages.Cancelled), (outcome.Status, outcome.Message));
        AssertUnchanged(old, fingerprint);
    }

    [Theory]
    [InlineData("import")]
    [InlineData("before-activation")]
    [InlineData("after-activation")]
    public async Task Should_RecoverOneWholeGeneration_When_TheAppStopsDuringImport(string boundary)
    {
        TabletSnapshot morning = Snapshots.Build("colm", locations: 4, products: 300);
        ActiveSnapshot old = await _tablet.SyncAsync(morning);
        string fingerprint = _tablet.SaveWork().Fingerprint();
        TabletSnapshot next = Snapshots.Build("colm", locations: 2, products: 400, label: "N");
        EncodedSnapshot encoded = EncodedSnapshot.From(next);
        string gzip = Path.Combine(_tablet.Root, "next.gz");
        await File.WriteAllBytesAsync(gzip, encoded.Gzip);

        await _tablet.KillAtAsync(boundary, "colm", "apply", gzip, encoded.GzipSha256, encoded.PlainSha256);

        // Reopen as the app would: the old snapshot or the new one, whole, and the work untouched.
        SnapshotStore store = _tablet.Sync().Store;
        ActiveSnapshot active = store.ReadActive()!;
        if (boundary == "after-activation") Assert.Equal(next.Manifest.Counts, active.Manifest.Counts);
        else Assert.Equal(old.Generation, active.Generation);
        Assert.Equal((boundary == "after-activation" ? next : morning).Locations.Select(row => row.Name),
            store.ReadLocations().Select(row => row.Name));
        Assert.Equal(fingerprint, new UnsentStore(_tablet.Workspace()).Fingerprint());
        Assert.DoesNotContain(store.Generations(), row => row.State == "staged");
    }

    [Theory]
    [InlineData("after-activation")]
    [InlineData("purge-batch")]
    public async Task Should_FinishCleanup_When_TheAppStopsAfterTheSwitch(string boundary)
    {
        // Enough rows that deleting the oldest copy takes several batches.
        TabletSnapshot oldest = Snapshots.Build("colm", locations: 50, products: 1500, pricesPerProduct: 3, label: "A");
        await _tablet.SyncAsync(oldest);
        ActiveSnapshot previous = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 5, products: 50, label: "B"));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        TabletSnapshot latest = Snapshots.Build("colm", locations: 3, products: 40, label: "C");
        if (boundary == "after-activation")
        {
            EncodedSnapshot encoded = EncodedSnapshot.From(latest);
            string gzip = Path.Combine(_tablet.Root, "latest.gz");
            await File.WriteAllBytesAsync(gzip, encoded.Gzip);
            await _tablet.KillAtAsync(boundary, "colm", "apply", gzip, encoded.GzipSha256, encoded.PlainSha256);
        }
        else
        {
            await _tablet.SyncAsync(latest);
            await _tablet.KillAtAsync(boundary, "colm", "purge");
        }

        SnapshotStore store = _tablet.Sync().Store;
        Assert.Equal(latest.Manifest.Counts, store.ReadActive()!.Manifest.Counts);
        Assert.Contains(store.Generations(), row => row.State == "retired");
        await _tablet.Sync().CleanupAsync();
        Assert.Equal([(previous.Generation, "complete", RowCount(Snapshots.Build("colm", 5, 50, label: "B")))],
            store.Generations().Where(row => row.Generation != store.ReadActive()!.Generation).Select(row => (row.Generation, row.State, row.Rows)));
        Assert.Equal(2, store.Generations().Count);
        Assert.Equal(fingerprint, new UnsentStore(_tablet.Workspace()).Fingerprint());
    }

    [Fact]
    public async Task Should_NeverAlterUnsentWork_When_ASnapshotIsDownloaded()
    {
        await _tablet.SyncAsync(Snapshots.Build("colm", locations: 4, products: 300));
        UnsentStore work = _tablet.SaveWork();
        string fingerprint = work.Fingerprint();
        string files = _tablet.UnsentFileHash();

        await _tablet.SyncAsync(Snapshots.Build("colm", locations: 6, products: 500, label: "B"));
        _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 6, 500, label: "C")), plainHash: new string('0', 64));
        Assert.Equal(SyncStatus.Failed, (await _tablet.Sync().SyncAsync()).Status);
        await _tablet.SyncAsync(Snapshots.Build("colm", locations: 1, products: 20, label: "D"));
        await _tablet.Sync().CleanupAsync();

        // A download never opens the Unsent database, so its files are byte-for-byte the same.
        Assert.Equal(files, _tablet.UnsentFileHash());
        Assert.Equal(fingerprint, work.Fingerprint());
        Assert.Equal([UnsentKind.Call, UnsentKind.Call, UnsentKind.Order], work.Read().Select(item => item.Kind));
    }

    [Fact]
    public async Task Should_RefuseSyncAndKeepData_When_FreeSpaceIsBelowTheCheck()
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 4, products: 300));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        int requests = _tablet.Server.Requests.Count;
        long required = 2 * _tablet.Workspace().SnapshotBytes() + TabletSync.ReserveBytes;
        _tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", locations: 2, products: 50, label: "N")));

        _tablet.Space.Available = required - 1;
        SyncOutcome refused = await _tablet.Sync().SyncAsync();
        Assert.Equal((SyncStatus.InsufficientSpace, SyncMessages.InsufficientSpace), (refused.Status, refused.Message));
        Assert.Equal(requests, _tablet.Server.Requests.Count);
        AssertUnchanged(old, fingerprint);

        _tablet.Space.Available = required;
        Assert.Equal(SyncStatus.Completed, (await _tablet.Sync().SyncAsync()).Status);
    }

    [Fact]
    public async Task Should_ReportUpdateRequired_When_TheServerSharesNoVersion()
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 20));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        _tablet.Server.Respond = _ => new(HttpStatusCode.NotAcceptable);
        SyncOutcome outcome = await _tablet.Sync().SyncAsync();
        Assert.Equal((SyncStatus.UpdateRequired, "Update the Field Sales app to sync"), (outcome.Status, outcome.Message));
        Assert.Equal("1", _tablet.Server.Requests[^1].Majors);
        AssertUnchanged(old, fingerprint);
    }

    [Theory]
    [InlineData("no-route")]
    [InlineData("timeout")]
    public async Task Should_ShowNoConnectionMessage_When_SyncHasNoSignal(string failure)
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 20));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        _tablet.Server.Respond = _ =>
        {
            if (failure == "no-route") throw new HttpRequestException(HttpRequestError.ConnectionError, "No such host is known.");
            throw new TaskCanceledException("The request timed out.");
        };
        SyncOutcome outcome = await _tablet.Sync().SyncAsync();
        Assert.Equal((SyncStatus.NoConnection, "No connection — your work is saved on this tablet"), (outcome.Status, outcome.Message));
        Assert.Equal(old.Generation, outcome.Active!.Generation);
        AssertUnchanged(old, fingerprint);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SyncStatus.SignInRequired)]
    [InlineData(HttpStatusCode.Forbidden, SyncStatus.NotPermitted)]
    public async Task Should_KeepDataAndSaySo_When_TheServerRefusesTheRep(HttpStatusCode refusal, SyncStatus expected)
    {
        ActiveSnapshot old = await _tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 20));
        string fingerprint = _tablet.SaveWork().Fingerprint();
        Assert.Equal("Bearer rep-token", _tablet.Server.Requests[^1].Authorization);
        _tablet.Server.Respond = _ => new(refusal);
        Assert.Equal(expected, (await _tablet.Sync().SyncAsync()).Status);
        AssertUnchanged(old, fingerprint);

        // Without a usable sign-in, Sync asks for one before contacting the server.
        int requests = _tablet.Server.Requests.Count;
        _tablet.Tokens.Token = null;
        Assert.Equal(SyncStatus.SignInRequired, (await _tablet.Sync().SyncAsync()).Status);
        Assert.Equal(requests, _tablet.Server.Requests.Count);
    }

    [Fact]
    public async Task Should_IgnoreAdditiveFields_When_TheServerSendsANewerMinorVersion()
    {
        // P3: a minor version only adds fields; this app reads 1.x and skips what it does not know.
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 300, version: "1.3");
        JsonObject json = JsonSerializer.SerializeToNode(snapshot, SnapshotContract.JsonOptions)!.AsObject();
        json["promotions"] = new JsonArray(Enumerable.Range(0, 2000).Select(index => (JsonNode)new JsonObject { ["id"] = index }).ToArray());
        json["campaign"] = "Autumn";
        json["locations"]![0]!["profile"] = "Pharmacy";
        _tablet.Server.Serve(EncodedSnapshot.FromJson(Encoding.UTF8.GetBytes(json.ToJsonString()), "1.3"));
        SyncOutcome outcome = await _tablet.Sync().SyncAsync();
        Assert.Equal(SyncStatus.Completed, outcome.Status);
        Assert.Equal(snapshot.Manifest.Counts, outcome.Active!.Manifest.Counts);
    }

    [Fact]
    public async Task Should_KeepEachRepsFilesApart_When_TwoRepsUseOneTablet()
    {
        await _tablet.SyncAsync(Snapshots.Build("colm", locations: 3, products: 20, label: "C"));
        await _tablet.SyncAsync(Snapshots.Build("aoife", locations: 1, products: 20, label: "A"), rep: "aoife");
        Assert.NotEqual(_tablet.Workspace("colm").DirectoryPath, _tablet.Workspace("aoife").DirectoryPath);
        Assert.Equal(3, _tablet.Sync("colm").Store.ReadLocations().Count);
        Assert.Equal(["A shop 0000"], _tablet.Sync("aoife").Store.ReadLocations().Select(row => row.Name));
    }

    [Fact]
    public void Should_LoadThePinnedNativeEngine_When_TheStoreOpens()
    {
        SqliteEngine engine = _tablet.Sync().Store.Engine;
        Assert.Equal(("3.53.3", "wal", 2L), (engine.Version, engine.JournalMode, engine.Synchronous));
    }

    [Theory]
    [InlineData("3.50.3", false)]
    [InlineData("3.53.2", false)]
    [InlineData("invalid", false)]
    [InlineData("3.53.3", true)]
    [InlineData("3.53.4", true)]
    [InlineData("3.54.0", true)]
    public void Should_RequireAtLeastMinimumSqliteVersion_When_CheckingEngineVersion(string version, bool expected)
    {
        Assert.Equal(expected, SqliteEngine.IsSupportedVersion(version));
    }

    [Fact]
    public void Should_RefuseToOpen_When_LocalDataIsFromANewerApp()
    {
        RepWorkspace workspace = _tablet.Workspace();
        _ = new SnapshotStore(workspace);
        using (SqliteConnection connection = new($"Data Source={workspace.SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version=3";
            command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => new SnapshotStore(workspace));
    }

    private void AssertUnchanged(ActiveSnapshot old, string fingerprint)
    {
        SnapshotStore store = _tablet.Sync().Store;
        ActiveSnapshot active = store.ReadActive()!;
        Assert.Equal((old.Generation, old.CompletedUtc, old.Manifest.Counts), (active.Generation, active.CompletedUtc, active.Manifest.Counts));
        Assert.Equal(old.Manifest.Counts.Locations, store.ReadLocations().Count);
        Assert.Equal(fingerprint, new UnsentStore(_tablet.Workspace()).Fingerprint());
    }

    private static long RowCount(TabletSnapshot snapshot) =>
        snapshot.Locations.Count + snapshot.Categories.Count + snapshot.Products.Count + snapshot.BasePrices.Count;

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
}
