using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

// WI-039 (T-4.1.2): the server receives each finished item exactly once and judges it as captured (BR-NEW-006).
[Collection(SqlServerCollection.Name)]
public sealed class TabletUploadApiTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private static readonly OrderSnapshotRef Morning = new("1.2", new DateTimeOffset(2026, 10, 1, 6, 42, 0, TimeSpan.Zero));
    private static readonly Guid Hickeys = new("3c0f6a52-0000-0000-0000-000000000001");
    private static readonly Guid VitaminD = new("6b2a9e11-0000-0000-0000-000000000001");
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await _app.ResetAsync();
        _initialized = true;
    }
    public Task DisposeAsync() => _initialized ? _app.ResetAsync() : Task.CompletedTask;

    [Fact]
    public async Task Should_StoreTheOrderAsReceivedAsPendingAndConfirmReceived_When_AReadyOrderIsUploadedFirst()
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD);
        string body = Json(order);

        using HttpResponseMessage response = await UploadAsync("colm", order.Id, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new UploadReceipt(order.Id, UploadReceiptStatus.Received),
            await response.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt));
        ReceivedWork work = Assert.Single(await WorkAsync());
        Assert.Equal((order.Id, ReceivedWorkKind.Order, "colm", body, Sha256(body), 0),
            (work.Id, work.Kind, work.RepSubject, work.Body, work.ContentSha256, work.ResentWithChangesCount));
        ReceivedOrder pending = Assert.Single(await OrdersAsync());
        Assert.Equal((order.Id, OrderLifecycleStatus.Pending, Hickeys, Hickeys, "colm", order.TakenOn, (Guid?)null, "1.2", Morning.BuiltUtc),
            (pending.Id, pending.Status, pending.LocationId, pending.OrderedById, pending.CapturingRep, pending.TakenOn, pending.CallId,
                pending.SnapshotVersion, pending.SnapshotBuiltUtc));
        Assert.Equal(work.ReceivedUtc, pending.ReceivedUtc);
    }

    [Fact]
    public async Task Should_StoreOnceAndConfirmAlreadyReceived_When_TheSameOrderIsSentAgain()
    {
        // The first confirmation was lost on the way back, so the tablet sends the same item at its next Sync.
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD);
        using HttpResponseMessage first = await UploadAsync("colm", order.Id, Json(order));
        using HttpResponseMessage again = await UploadAsync("colm", order.Id, Json(order));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, again.StatusCode));
        Assert.Equal(new UploadReceipt(order.Id, UploadReceiptStatus.AlreadyReceived),
            await again.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt));
        Assert.Equal(0, Assert.Single(await WorkAsync()).ResentWithChangesCount);
        Assert.Single(await OrdersAsync());
    }

    [Fact]
    public async Task Should_KeepTheFirstVersionAndRecordTheResend_When_TheSameIdArrivesWithDifferentContent()
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD);
        string firstBody = Json(order);
        TabletOrder changed = order with { Lines = [order.Lines[0] with { Quantity = 24m }] };
        using HttpResponseMessage first = await UploadAsync("colm", order.Id, firstBody);

        using HttpResponseMessage again = await UploadAsync("colm", order.Id, Json(changed));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(UploadReceiptStatus.AlreadyReceived,
            (await again.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt))!.Status);
        ReceivedWork work = Assert.Single(await WorkAsync());
        Assert.Equal((firstBody, 1), (work.Body, work.ResentWithChangesCount));
        Assert.NotNull(work.LastResentWithChangesUtc);
        Assert.Single(await OrdersAsync());
    }

    [Fact]
    public async Task Should_StoreOnce_When_TheSameOrderArrivesTwiceAtOnce()
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD);
        string body = Json(order);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => UploadAsync("colm", order.Id, body)));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.Created),
                response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally { foreach (HttpResponseMessage response in responses) response.Dispose(); }
        Assert.Single(await WorkAsync());
        Assert.Single(await OrdersAsync());
    }

    [Fact]
    public async Task Should_AcceptAsPendingWithTheLineUnchanged_When_AProductWasArchivedAfterTheSnapshot()
    {
        // Products have no archive, Range or availability state yet. Moving the product into a Restriction Group that is
        // then archived hides it from every rep (WI-011): that is how a product becomes unavailable after a snapshot today.
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid syrupId = await _app.ProductAsync("ACS-100", "Autumn Cough Syrup 100ml", remedies.Id, price: 6.40m);
        TabletSnapshot morning = await _app.SyncAsync("colm");
        SnapshotProduct syrup = Assert.Single(morning.Products, product => product.Id == syrupId);

        RestrictionGroup seasonal = await _app.RestrictionGroupAsync("Seasonal remedies");
        await _app.RestrictAsync(syrupId, seasonal.Id);
        await _app.ArchiveGroupAsync(seasonal.Id);
        Assert.DoesNotContain((await _app.SyncAsync("colm")).Products, product => product.Id == syrupId);

        OrderSnapshotRef captured = new(morning.Manifest.Version, morning.Manifest.BuiltUtc);
        TabletOrder order = Order(Guid.NewGuid(), hickeys, syrup.Id, snapshot: captured, code: syrup.Code, name: syrup.Name, price: 6.40m);
        string body = Json(order);
        using HttpResponseMessage response = await UploadAsync("colm", order.Id, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(OrderLifecycleStatus.Pending, Assert.Single(await OrdersAsync()).Status);
        TabletOrder stored = JsonSerializer.Deserialize(Assert.Single(await WorkAsync()).Body, OrderJsonContext.Default.TabletOrder)!;
        Assert.Equal(JsonSerializer.Serialize(order.Lines[0], OrderJsonContext.Default.OrderLine),
            JsonSerializer.Serialize(Assert.Single(stored.Lines), OrderJsonContext.Default.OrderLine));
    }

    [Theory]
    [InlineData("reassigned")]
    [InlineData("town archived")]
    public async Task Should_AcceptTheOrder_When_TheShopWasReassignedOrArchivedAfterCapture(string change)
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid quinns = await _app.CustomerAsync("Quinn's Centra", places.Arklow.Id);
        var assignment = await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        TabletSnapshot morning = await _app.SyncAsync("colm");
        Assert.Contains(morning.Locations, location => location.Id == quinns);

        if (change == "reassigned")
        {
            await _app.RemoveAssignmentAsync(assignment.Id);
            await _app.AssignAsync("aoife", TerritoryLevel.Town, places.Arklow.Id);
            Assert.DoesNotContain((await _app.SyncAsync("colm")).Locations, location => location.Id == quinns);
        }
        else await _app.ArchiveTownAsync(places.Arklow.Id);

        TabletOrder order = Order(Guid.NewGuid(), quinns, VitaminD, snapshot: new(morning.Manifest.Version, morning.Manifest.BuiltUtc));
        using HttpResponseMessage response = await UploadAsync("colm", order.Id, Json(order));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ReceivedOrder pending = Assert.Single(await OrdersAsync());
        Assert.Equal((quinns, OrderLifecycleStatus.Pending), (pending.LocationId, pending.Status));
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager, true, HttpStatusCode.Forbidden)]
    [InlineData(BusinessRoles.HeadOfficeUser, true, HttpStatusCode.Forbidden)]
    [InlineData(BusinessRoles.FieldSalesperson, false, HttpStatusCode.Forbidden)]
    [InlineData(null, true, HttpStatusCode.Unauthorized)]
    public async Task Should_RefuseUpload_When_TheCallerIsNotACurrentFieldSalesperson(string? role, bool withScope, HttpStatusCode expected)
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD, rep: "niamh");
        using HttpClient client = role is null ? _app.Anonymous() : _app.ClientAs("niamh", [role!], withScope);

        using HttpResponseMessage response = await client.PutAsync(TabletUploadContract.OrderPath(order.Id), Content(Json(order)));

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await WorkAsync());
        Assert.Empty(await OrdersAsync());
    }

    [Fact]
    public async Task Should_StoreTheCallAsReceivedAndConfirmOnce_When_ASavedCallIsUploadedTwice()
    {
        // Record Call (WI-047) defines a Call's contents; until then the server stores whatever object the tablet saved.
        Guid id = Guid.NewGuid();
        string body = $$"""{"id":"{{id}}","locationId":"{{Hickeys}}","channel":"InPerson","pitchNotes":"Autumn range"}""";

        using HttpResponseMessage first = await UploadAsync("colm", id, body, TabletUploadContract.CallPath(id));
        using HttpResponseMessage again = await UploadAsync("colm", id, body, TabletUploadContract.CallPath(id));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, again.StatusCode));
        Assert.Equal(new UploadReceipt(id, UploadReceiptStatus.Received),
            await first.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt));
        Assert.Equal(new UploadReceipt(id, UploadReceiptStatus.AlreadyReceived),
            await again.Content.ReadFromJsonAsync(UploadJsonContext.Default.UploadReceipt));
        ReceivedWork work = Assert.Single(await WorkAsync());
        Assert.Equal((ReceivedWorkKind.Call, "colm", body, 0), (work.Kind, work.RepSubject, work.Body, work.ResentWithChangesCount));
        Assert.Empty(await OrdersAsync());
    }

    [Theory]
    [InlineData("order", "not json", UploadFaults.Corrupt)]
    [InlineData("order", "[]", UploadFaults.Corrupt)]
    [InlineData("order", "unknown state", UploadFaults.Corrupt)]
    [InlineData("order", "other schema", UploadFaults.Unsupported)]
    [InlineData("order", "newer version", UploadFaults.Unsupported)]
    [InlineData("order", "id mismatch", UploadFaults.Incomplete)]
    [InlineData("order", "in progress", UploadFaults.Incomplete)]
    [InlineData("order", "no lines", UploadFaults.Incomplete)]
    [InlineData("order", "no snapshot", UploadFaults.Incomplete)]
    [InlineData("order", "zero quantity", UploadFaults.Incomplete)]
    [InlineData("call", "not json", UploadFaults.Corrupt)]
    [InlineData("call", "[]", UploadFaults.Corrupt)]
    [InlineData("call", "id mismatch", UploadFaults.Incomplete)]
    public async Task Should_RefuseWithoutStoring_When_TheBodyIsCorruptIncompleteOrUnsupported(string kind, string fault, string expected)
    {
        Guid id = Guid.NewGuid();
        TabletOrder order = Order(id, Hickeys, VitaminD);
        string body = (kind, fault) switch
        {
            (_, "not json") => "{\"id\":",
            (_, "[]") => "[]",
            ("order", "unknown state") => Json(order).Replace("\"ReadyToSend\"", "\"Shipped\"", StringComparison.Ordinal),
            ("order", "other schema") => Json(order with { Schema = "field-sales-call" }),
            ("order", "newer version") => Json(order with { Version = OrderContract.CurrentVersion + 1 }),
            ("order", "id mismatch") => Json(order with { Id = Guid.NewGuid() }),
            ("order", "in progress") => Json(order with { State = OrderState.InProgress }),
            ("order", "no lines") => Json(order with { Lines = [] }),
            ("order", "no snapshot") => Json(order with { Snapshot = null! }),
            ("order", "zero quantity") => Json(order with { Lines = [order.Lines[0] with { Quantity = 0m }] }),
            ("call", "id mismatch") => $$"""{"id":"{{Guid.NewGuid()}}"}""",
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };

        using HttpResponseMessage response = await UploadAsync("colm", id, body, kind == "order" ? null : TabletUploadContract.CallPath(id));

        Assert.Equal((HttpStatusCode.BadRequest, expected), (response.StatusCode, await FaultAsync(response)));
        Assert.Empty(await WorkAsync());
        Assert.Empty(await OrdersAsync());
    }

    [Fact]
    public async Task Should_RefuseWithoutStoring_When_TheOrderNamesAnotherCapturingRep()
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD, rep: "aoife");

        using HttpResponseMessage response = await UploadAsync("colm", order.Id, Json(order));

        Assert.Equal((HttpStatusCode.BadRequest, UploadFaults.NotYours), (response.StatusCode, await FaultAsync(response)));
        Assert.Empty(await WorkAsync());
    }

    [Theory]
    [InlineData("another rep")]
    [InlineData("another kind")]
    public async Task Should_RefuseWithoutConfirming_When_AnotherRepAlreadyUsedTheId(string clash)
    {
        TabletOrder colms = Order(Guid.NewGuid(), Hickeys, VitaminD);
        using HttpResponseMessage stored = await UploadAsync("colm", colms.Id, Json(colms));
        Assert.Equal(HttpStatusCode.Created, stored.StatusCode);

        using HttpResponseMessage response = clash == "another rep"
            ? await UploadAsync("aoife", colms.Id, Json(colms with { CapturingRep = "aoife" }))
            : await UploadAsync("colm", colms.Id, $$"""{"id":"{{colms.Id}}"}""", TabletUploadContract.CallPath(colms.Id));

        Assert.Equal((HttpStatusCode.Conflict, UploadFaults.IdInUse), (response.StatusCode, await FaultAsync(response)));
        ReceivedWork work = Assert.Single(await WorkAsync());
        Assert.Equal(("colm", ReceivedWorkKind.Order, Json(colms), 0), (work.RepSubject, work.Kind, work.Body, work.ResentWithChangesCount));
    }

    [Fact]
    public async Task Should_RefuseWithoutStoring_When_AnItemIsLargerThanTheLimit()
    {
        TabletOrder order = Order(Guid.NewGuid(), Hickeys, VitaminD);
        string json = Json(order);
        // Valid JSON padded with whitespace to one byte over the limit.
        string body = json + new string(' ', TabletUploadContract.MaximumBodyBytes - Encoding.UTF8.GetByteCount(json) + 1);

        using HttpResponseMessage response = await UploadAsync("colm", order.Id, body);
        using HttpResponseMessage atLimit = await UploadAsync("colm", order.Id, body[..^1]);

        Assert.Equal((HttpStatusCode.RequestEntityTooLarge, UploadFaults.TooLarge), (response.StatusCode, await FaultAsync(response)));
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    private static async Task<string?> FaultAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.TryGetProperty(UploadFaults.ProblemKey, out JsonElement fault) ? fault.GetString() : null;
    }

    private static TabletOrder Order(Guid id, Guid locationId, Guid productId, string rep = "colm", OrderSnapshotRef? snapshot = null,
        string code = "VD-1000", string name = "Vitamin D 1000IU 90s", decimal price = 8.95m)
    {
        OrderSnapshotRef source = snapshot ?? Morning;
        OrderLine line = new(Guid.NewGuid(), productId, code, name, 12m, "Each",
            new OrderLinePrice(price, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                [new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, price, true)]), source);
        return new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, locationId, locationId, rep,
            new DateOnly(2026, 10, 1), null, source, [line]);
    }

    private static string Json(TabletOrder order) => JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder);

    private static StringContent Content(string body) => new(body, Encoding.UTF8, TabletUploadContract.MediaType);

    private static string Sha256(string body) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    private async Task<HttpResponseMessage> UploadAsync(string rep, Guid id, string body, string? path = null)
    {
        using HttpClient client = _app.ClientAs(rep, [BusinessRoles.FieldSalesperson]);
        return await client.PutAsync(path ?? TabletUploadContract.OrderPath(id), Content(body));
    }

    private async Task<ReceivedWork[]> WorkAsync()
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().ReceivedWork.AsNoTracking().ToArrayAsync();
    }

    private async Task<ReceivedOrder[]> OrdersAsync()
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.AsNoTracking().ToArrayAsync();
    }
}
