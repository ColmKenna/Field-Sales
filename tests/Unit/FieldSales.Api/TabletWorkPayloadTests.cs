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

    // WI-049 scenario 16: each stock entry names one product with its captured code, name and a known unit, and its count
    // passes the same check the tablet used. Everything else about the product is judged as captured.
    [Theory]
    [InlineData("negative count", "Enter 0 or more")]
    [InlineData("fraction of an item", "Enter a whole number")]
    [InlineData("too precise", "Enter a count with up to six decimal places")]
    [InlineData("duplicate product", "A stock entry is incomplete.")]
    [InlineData("no product", "A stock entry is incomplete.")]
    [InlineData("no name", "A stock entry is incomplete.")]
    [InlineData("unknown unit", "A stock entry is incomplete.")]
    [InlineData("all not checked", CallContract.RecordSomething)]
    public void Should_RefuseTheCallAsIncomplete_When_AStockEntryIsMalformed(string fault, string detail)
    {
        Guid id = Guid.NewGuid();
        CallStockEntry counted = new(Product, "VD-1000", "Vitamin D 1000IU 90s", "Each", 4m);
        CallStockEntry oats = new(Guid.NewGuid(), "OAT-1", "Loose Porridge Oats", "kg", 3.5m);
        CallStockEntry[] entries = fault switch
        {
            "negative count" => [counted with { Count = -2m }],
            "fraction of an item" => [counted with { Count = 2.5m }],
            "too precise" => [oats with { Count = 0.0000001m }],
            "duplicate product" => [counted, counted with { Count = null }],
            "no product" => [counted with { ProductId = Guid.Empty }],
            "no name" => [counted with { ProductName = " " }],
            "unknown unit" => [counted with { Unit = "crate" }],
            "all not checked" => [counted with { Count = null }],
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        TabletCall call = Call(id) with { PitchNotes = null, StockEntries = entries };

        TabletCall? read = TabletWorkReceiver.ReadCall(id, Body(call), out ReceiveResult? refused);

        Assert.Null(read);
        Assert.Equal((ReceiveOutcome.Refused, UploadFaults.Incomplete), (refused!.Outcome, refused.Fault));
        Assert.Contains(detail, refused.Detail);
        // The same entries with a valid count are accepted: a measured product counts in decimals with its unit.
        TabletCall valid = Call(id) with { PitchNotes = null, StockEntries = [counted, oats, counted with { ProductId = Guid.NewGuid(), Count = null }] };
        Assert.NotNull(TabletWorkReceiver.ReadCall(id, Body(valid), out _));
    }

    // WI-050 scenario 12 (D8): a phone Call carries only mentioned products, each with Low or a count; Out is Low with 0.
    [Fact]
    public void Should_AcceptLowMarksAndRefuseAnUntouchedPhoneEntry_When_ACallIsReceived()
    {
        Guid id = Guid.NewGuid();
        CallStockEntry low = new(Product, "SPF-30", "SPF30 Sun Lotion 200ml", "Each", null, true);
        CallStockEntry @out = new(Guid.NewGuid(), "SUD-125", "Sudocrem 125g", "Each", 0m, true);
        TabletCall phone = Call(id) with { Channel = CallChannel.Phone, PitchNotes = null, StockEntries = [low, @out] };

        TabletCall? accepted = TabletWorkReceiver.ReadCall(id, Body(phone), out ReceiveResult? none);

        Assert.Null(none);
        Assert.Equal([low, @out], accepted!.StockEntries);
        Assert.Equal("Phone · 2 marked Low (1 out)", accepted.Content.Summary);
        // Low is written only when true, so an unmarked entry's body is unchanged from WI-049's.
        string json = Encoding.UTF8.GetString(Body(phone with { StockEntries = [low with { Low = false, Count = 3m }] }));
        Assert.DoesNotContain("\"low\"", json);

        TabletCall untouched = phone with { StockEntries = [low, new(Guid.NewGuid(), "P-01", "Product 01", "Each")] };
        Assert.Null(TabletWorkReceiver.ReadCall(id, Body(untouched), out ReceiveResult? refused));
        Assert.Equal((ReceiveOutcome.Refused, UploadFaults.Incomplete), (refused!.Outcome, refused.Fault));
        Assert.Contains(CallContract.MentionedOnly, refused.Detail);
        // In person the same untouched product is Not checked, and a Low mark alone records something there too (D3, D5).
        Assert.NotNull(TabletWorkReceiver.ReadCall(id, Body(untouched with { Channel = CallChannel.InPerson }), out _));
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
