using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldSales.Ordering.Contracts;

/// <summary>The order record the tablet captures and the server accepts unchanged (T-5.1.1, hand-off to T-4.1.2).</summary>
/// <remarks>
/// One type serves both sides, so an uploaded order needs no mapping. The tablet stores the serialized body as an
/// Unsent Order. <c>locationId</c> stays a top-level property because the tablet reads it from any Unsent item to
/// keep that shop's details. A captured price or quantity never changes after capture; later pricing (E6) adds
/// price candidates without changing this shape. Add fields only as optional, additive properties.
/// </remarks>
public static class OrderContract
{
    public const string Schema = "field-sales-order";
    public const int CurrentVersion = 1;

    /// <summary>The kind and label of the only price source until the full price engine arrives.</summary>
    public const string BasePriceKind = "Base";
    public const string BasePriceLabel = "Base price";

    /// <summary>Web defaults with source-generated metadata, so trimmed tablet Release builds keep (de)serialization.</summary>
    public static JsonSerializerOptions JsonOptions => OrderJsonContext.Default.Options;
}

/// <summary>The tablet states an order can hold before upload. Sent and Needs Attention arrive with Sync.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderState>))]
public enum OrderState { InProgress, ReadyToSend }

/// <summary>The snapshot a value was captured against: its contract version and the moment it was built.</summary>
public sealed record OrderSnapshotRef(string Version, DateTimeOffset BuiltUtc);

/// <summary>One price considered for a line. Only the base price is a candidate until E6.</summary>
public sealed record OrderPriceCandidate(string Kind, string Label, decimal Amount, bool IsWinner);

/// <summary>The price a line captured, with where it came from and every candidate considered.</summary>
public sealed record OrderLinePrice(decimal Amount, string Kind, string Label, IReadOnlyList<OrderPriceCandidate> Candidates);

/// <summary>A product on an order. The product code and name are kept as captured, so the line still reads
/// correctly if the product is later renamed or hidden. Quantity is in <see cref="Unit"/>.</summary>
public sealed record OrderLine(Guid Id, Guid ProductId, string ProductCode, string ProductName, decimal Quantity,
    string Unit, OrderLinePrice Price, OrderSnapshotRef Snapshot);

/// <summary>An order taken for one Location. For an ordinary order the Location it counts at
/// (<see cref="OrderedById"/>) is the Location it is for (<see cref="LocationId"/>).</summary>
public sealed record TabletOrder(string Schema, int Version, Guid Id, OrderState State, Guid LocationId,
    Guid OrderedById, string CapturingRep, DateOnly TakenOn, Guid? CallId, OrderSnapshotRef Snapshot,
    IReadOnlyList<OrderLine> Lines);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TabletOrder))]
[JsonSerializable(typeof(OrderLine))]
[JsonSerializable(typeof(OrderLinePrice))]
[JsonSerializable(typeof(OrderPriceCandidate))]
[JsonSerializable(typeof(OrderSnapshotRef))]
public sealed partial class OrderJsonContext : JsonSerializerContext;
