using System.Text;
using System.Text.Json;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Api.Tests;

/// <summary>Structure checks on an uploaded item (T-4.1.2), without the database. The SQL-backed
/// TabletUploadApiTests still prove a refused item is never stored and a valid one is stored once.</summary>
[Trait("Category", "Unit")]
public sealed class TabletWorkPayloadTests
{
    private static readonly OrderSnapshotRef Morning = new("1.2", new DateTimeOffset(2026, 10, 1, 6, 42, 0, TimeSpan.Zero));
    private static readonly Guid Shop = new("3c0f6a52-0000-0000-0000-000000000001");
    private static readonly Guid Product = new("6b2a9e11-0000-0000-0000-000000000001");

    [Fact]
    public void Should_ReturnTheOrder_When_AReadyOrderIsComplete()
    {
        Guid id = Guid.NewGuid();

        TabletOrder? order = TabletWorkReceiver.ReadOrder(id, Body(Order(id)), out ReceiveResult? refused);

        Assert.Null(refused);
        Assert.Equal((id, OrderState.ReadyToSend, "colm"), (order!.Id, order.State, order.CapturingRep));
    }

    [Theory]
    [InlineData("not json", UploadFaults.Corrupt)]
    [InlineData("[]", UploadFaults.Corrupt)]
    [InlineData("unknown state", UploadFaults.Corrupt)]
    [InlineData("other schema", UploadFaults.Unsupported)]
    [InlineData("newer version", UploadFaults.Unsupported)]
    [InlineData("version zero", UploadFaults.Unsupported)]
    [InlineData("id mismatch", UploadFaults.Incomplete)]
    [InlineData("in progress", UploadFaults.Incomplete)]
    [InlineData("no location", UploadFaults.Incomplete)]
    [InlineData("no ordered by", UploadFaults.Incomplete)]
    [InlineData("no rep", UploadFaults.Incomplete)]
    [InlineData("no date", UploadFaults.Incomplete)]
    [InlineData("no snapshot", UploadFaults.Incomplete)]
    [InlineData("snapshot version too long", UploadFaults.Incomplete)]
    [InlineData("snapshot without build time", UploadFaults.Incomplete)]
    [InlineData("no lines", UploadFaults.Incomplete)]
    [InlineData("zero quantity", UploadFaults.Incomplete)]
    [InlineData("line without product", UploadFaults.Incomplete)]
    [InlineData("line without unit", UploadFaults.Incomplete)]
    [InlineData("negative price", UploadFaults.Incomplete)]
    [InlineData("line without snapshot", UploadFaults.Incomplete)]
    public void Should_RefuseTheOrder_When_TheBodyIsCorruptUnsupportedOrIncomplete(string fault, string expected)
    {
        Guid id = Guid.NewGuid();
        TabletOrder order = Order(id);
        OrderLine line = order.Lines[0];
        string body = fault switch
        {
            "not json" => "{\"id\":",
            "[]" => "[]",
            "unknown state" => Json(order).Replace("\"ReadyToSend\"", "\"Shipped\"", StringComparison.Ordinal),
            "other schema" => Json(order with { Schema = "field-sales-call" }),
            "newer version" => Json(order with { Version = OrderContract.CurrentVersion + 1 }),
            "version zero" => Json(order with { Version = 0 }),
            "id mismatch" => Json(order with { Id = Guid.NewGuid() }),
            "in progress" => Json(order with { State = OrderState.InProgress }),
            "no location" => Json(order with { LocationId = Guid.Empty }),
            "no ordered by" => Json(order with { OrderedById = Guid.Empty }),
            "no rep" => Json(order with { CapturingRep = " " }),
            "no date" => Json(order with { TakenOn = default }),
            "no snapshot" => Json(order with { Snapshot = null! }),
            "snapshot version too long" => Json(order with { Snapshot = Morning with { Version = new string('1', 21) } }),
            "snapshot without build time" => Json(order with { Snapshot = Morning with { BuiltUtc = default } }),
            "no lines" => Json(order with { Lines = [] }),
            "zero quantity" => Json(order with { Lines = [line with { Quantity = 0m }] }),
            "line without product" => Json(order with { Lines = [line with { ProductId = Guid.Empty }] }),
            "line without unit" => Json(order with { Lines = [line with { Unit = "" }] }),
            "negative price" => Json(order with { Lines = [line with { Price = line.Price with { Amount = -0.01m } }] }),
            "line without snapshot" => Json(order with { Lines = [line, line with { Id = Guid.NewGuid(), Snapshot = null! }] }),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };

        TabletOrder? read = TabletWorkReceiver.ReadOrder(id, Encoding.UTF8.GetBytes(body), out ReceiveResult? refused);

        Assert.Null(read);
        Assert.Equal((ReceiveOutcome.Refused, expected), (refused!.Outcome, refused.Fault));
        Assert.False(string.IsNullOrWhiteSpace(refused.Detail));
    }

    private static TabletCall Call(Guid id) => new(CallContract.Schema, CallContract.CurrentVersion, id, Shop, "colm",
        new DateOnly(2026, 9, 22), new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.FromHours(1)), CallChannel.InPerson,
        new(Morning.Version, Morning.BuiltUtc), "Presented autumn cold & flu range");

    private static byte[] Body(TabletCall call) => JsonSerializer.SerializeToUtf8Bytes(call, CallJsonContext.Default.TabletCall);

    [Fact]
    public void Should_AcceptTheCall_When_ItIsARecordedCall()
    {
        Guid id = Guid.NewGuid();

        TabletCall? call = TabletWorkReceiver.ReadCall(id, Body(Call(id)), out ReceiveResult? refused);

        Assert.Null(refused);
        Assert.Equal((id, Shop, CallChannel.InPerson, "Presented autumn cold & flu range"),
            (call!.Id, call.LocationId, call.Channel, call.PitchNotes));
    }

    // WI-047 scenario 13: a Call is refused for its own structure only, never for what changed since the snapshot.
    [Theory]
    [InlineData("not json", UploadFaults.Corrupt)]
    [InlineData("array", UploadFaults.Corrupt)]
    [InlineData("unknown channel", UploadFaults.Corrupt)]
    [InlineData("other schema", UploadFaults.Unsupported)]
    [InlineData("newer version", UploadFaults.Unsupported)]
    [InlineData("id mismatch", UploadFaults.Incomplete)]
    [InlineData("no location", UploadFaults.Incomplete)]
    [InlineData("no rep", UploadFaults.Incomplete)]
    [InlineData("no date", UploadFaults.Incomplete)]
    [InlineData("no channel", UploadFaults.Incomplete)]
    [InlineData("no snapshot", UploadFaults.Incomplete)]
    [InlineData("nothing recorded", UploadFaults.Incomplete)]
    [InlineData("notes too long", UploadFaults.Incomplete)]
    public void Should_RefuseTheCall_When_ItsStructureIsWrong(string fault, string expected)
    {
        Guid id = Guid.NewGuid();
        TabletCall call = Call(id);
        string json = Encoding.UTF8.GetString(Body(call));
        string body = fault switch
        {
            "not json" => "{\"id\":",
            "array" => "[]",
            "unknown channel" => json.Replace("\"InPerson\"", "\"Carrier pigeon\"", StringComparison.Ordinal),
            "other schema" => Json(call with { Schema = OrderContract.Schema }),
            "newer version" => Json(call with { Version = CallContract.CurrentVersion + 1 }),
            "id mismatch" => Json(call with { Id = Guid.NewGuid() }),
            "no location" => Json(call with { LocationId = Guid.Empty }),
            "no rep" => Json(call with { CapturingRep = " " }),
            "no date" => Json(call with { CalledOn = default }),
            "no channel" => json.Replace("\"channel\":\"InPerson\",", "", StringComparison.Ordinal),
            "no snapshot" => Json(call with { Snapshot = null! }),
            "nothing recorded" => Json(call with { PitchNotes = null }),
            "notes too long" => Json(call with { PitchNotes = new string('x', CallContract.MaximumPitchNotesLength + 1) }),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };

        TabletCall? read = TabletWorkReceiver.ReadCall(id, Encoding.UTF8.GetBytes(body), out ReceiveResult? refused);

        Assert.Null(read);
        Assert.Equal((ReceiveOutcome.Refused, expected), (refused!.Outcome, refused.Fault));
        Assert.False(string.IsNullOrWhiteSpace(refused.Detail));

        static string Json(TabletCall changed) => JsonSerializer.Serialize(changed, CallJsonContext.Default.TabletCall);
    }

    private static TabletOrder Order(Guid id)
    {
        OrderLine line = new(Guid.NewGuid(), Product, "VD-1000", "Vitamin D 1000IU 90s", 12m, "Each",
            new OrderLinePrice(8.95m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                [new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 8.95m, true)]), Morning);
        return new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, Shop, Shop, "colm",
            new DateOnly(2026, 10, 1), null, Morning, [line]);
    }

    private static string Json(TabletOrder order) => JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder);

    private static byte[] Body(TabletOrder order) => Encoding.UTF8.GetBytes(Json(order));
}
