using System.Text.Json;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Api.Tests;

/// <summary>WI-038 scenarios 10-11: the server reads an order the tablet captured with the same model, unchanged.
/// The body below is what the tablet stores for an Unsent Order and will upload (WI-039 / T-4.1.2).</summary>
[Trait("Category", "Unit")]
public sealed class TabletOrderContractTests
{
    private static readonly string TabletBody = """
        {"schema":"field-sales-order","version":1,"id":"7a1f2b30-0000-0000-0000-000000000001","state":"ReadyToSend",
        "locationId":"55000000-0000-0000-0000-000000000001","orderedById":"55000000-0000-0000-0000-000000000001",
        "capturingRep":"colm","takenOn":"2026-10-29","callId":null,"snapshot":{"version":"1.2","builtUtc":"2026-10-29T06:30:00+00:00"},
        "lines":[{"id":"8b2e3c40-0000-0000-0000-000000000001","productId":"66000000-0000-0000-0000-000000000001",
        "productCode":"VITD-1000-90","productName":"Vitamin D 1000IU 90s","quantity":12,"unit":"Each",
        "price":{"amount":12.50,"kind":"Base","label":"Base price","candidates":[{"kind":"Base","label":"Base price","amount":12.50,"isWinner":true}]},
        "snapshot":{"version":"1.2","builtUtc":"2026-10-29T06:30:00+00:00"}},
        {"id":"8b2e3c40-0000-0000-0000-000000000002","productId":"66000000-0000-0000-0000-000000000002",
        "productCode":"TEA-LOOSE","productName":"Loose Herbal Tea","quantity":1.5,"unit":"kg",
        "price":{"amount":4.80,"kind":"Base","label":"Base price","candidates":[{"kind":"Base","label":"Base price","amount":4.80,"isWinner":true}]},
        "snapshot":{"version":"1.2","builtUtc":"2026-10-29T06:30:00+00:00"}}]}
        """.ReplaceLineEndings("");

    [Fact]
    public void Should_ReadEveryFieldIntact_When_TheServerModelReadsTheTabletBody()
    {
        TabletOrder order = JsonSerializer.Deserialize(TabletBody, OrderJsonContext.Default.TabletOrder)!;

        Assert.Equal((OrderContract.Schema, OrderContract.CurrentVersion), (order.Schema, order.Version));
        Assert.Equal(Guid.Parse("7a1f2b30-0000-0000-0000-000000000001"), order.Id);
        Assert.Equal(OrderState.ReadyToSend, order.State);
        Assert.Equal(Guid.Parse("55000000-0000-0000-0000-000000000001"), order.LocationId);
        Assert.Equal(order.LocationId, order.OrderedById);
        Assert.Equal("colm", order.CapturingRep);
        Assert.Null(order.CallId);
        Assert.Equal(new OrderSnapshotRef("1.2", new DateTimeOffset(2026, 10, 29, 6, 30, 0, TimeSpan.Zero)), order.Snapshot);
        Assert.Equal(2, order.Lines.Count);
        OrderLine first = order.Lines[0];
        Assert.Equal(("VITD-1000-90", "Vitamin D 1000IU 90s", 12m, "Each"), (first.ProductCode, first.ProductName, first.Quantity, first.Unit));
        Assert.Equal((12.50m, "Base", "Base price"), (first.Price.Amount, first.Price.Kind, first.Price.Label));
        Assert.True(Assert.Single(first.Price.Candidates).IsWinner);
        Assert.Equal(order.Snapshot, first.Snapshot);
        Assert.Equal(("kg", 1.5m), (order.Lines[1].Unit, order.Lines[1].Quantity));
    }

    [Fact]
    public void Should_WriteBackTheSameBodyWithPriceQuantityAndDatesExact_When_TheModelRoundTripsIt()
    {
        TabletOrder order = JsonSerializer.Deserialize(TabletBody, OrderJsonContext.Default.TabletOrder)!;

        Assert.Equal(TabletBody, JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder));
        Assert.Equal(new DateOnly(2026, 10, 29), order.TakenOn);
        Assert.Equal(12.50m, order.Lines[0].Price.Amount);
        Assert.Equal(4.80m, order.Lines[1].Price.Amount);
    }

    [Fact]
    public void Should_StillReadTheOrder_When_AFutureVersionAddsAnOptionalField()
    {
        string future = TabletBody.Replace("\"callId\":null", "\"callId\":null,\"approvalNote\":\"added later\"");

        TabletOrder order = JsonSerializer.Deserialize(future, OrderJsonContext.Default.TabletOrder)!;

        Assert.Equal(TabletBody, JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder));
    }
}
