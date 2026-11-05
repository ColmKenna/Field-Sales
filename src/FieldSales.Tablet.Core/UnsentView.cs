using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

public enum UnsentSection { NeedsAttention, ReadyToSend, InProgress, Other }

/// <summary>The captured work T-01 can name and review, without exposing its raw JSON or server response.</summary>
public sealed record UnsentView(UnsentItem Item, string LocationName, TabletOrder? Order, string? CallChannel)
{
    public IReadOnlySet<Guid> MissingProductIds { get; init; } = new HashSet<Guid>();
    /// <summary>The shop the item is for, when its body names one (a Call's page is reached through it, T-5.6.1).</summary>
    public Guid? LocationId { get; init; }
    public string KindLabel => Item.Kind == UnsentKind.ScheduleChange ? "Schedule change" : Item.Kind.ToString();
    public UnsentSection Section
    {
        get
        {
            if (Item.Rejection is not null) return UnsentSection.NeedsAttention;
            if (Item.Kind == UnsentKind.Call) return UnsentSection.ReadyToSend;
            if (Item.Kind == UnsentKind.Order)
            {
                using JsonDocument body = JsonDocument.Parse(Item.Json);
                if (body.RootElement.TryGetProperty("state", out JsonElement state) && state.ValueKind == JsonValueKind.String)
                {
                    if (state.ValueEquals(nameof(OrderState.ReadyToSend))) return UnsentSection.ReadyToSend;
                    if (state.ValueEquals(nameof(OrderState.InProgress))) return UnsentSection.InProgress;
                }
            }
            return UnsentSection.Other;
        }
    }

    public bool CanMarkReady => Item.Kind == UnsentKind.Order && Order is { Lines.Count: > 0 } order
        && (Item.Rejection is null ? order.State == OrderState.InProgress : order.State == OrderState.ReadyToSend);
}

public sealed partial class UnsentStore
{
    public IReadOnlyList<UnsentView> ReadViews()
    {
        if (!File.Exists(workspace.UnsentPath)) return [];
        SnapshotStore? snapshot = File.Exists(workspace.SnapshotPath) ? new(workspace) : null;
        IReadOnlySet<Guid> productIds = snapshot?.ReadProductIds() ?? new HashSet<Guid>();
        return Read().OrderBy(item => item.SavedUtc).ThenBy(item => item.Id).Select(item =>
        {
            using JsonDocument body = JsonDocument.Parse(item.Json);
            JsonElement root = body.RootElement;
            Guid? locationId = root.TryGetProperty("locationId", out JsonElement shop) && shop.ValueKind == JsonValueKind.String
                && shop.TryGetGuid(out Guid id) ? id : null;
            string location = locationId is Guid known
                ? snapshot?.ReadLocation(known)?.Location.Name ?? "Shop details unavailable" : "Shop details unavailable";
            string? channel = item.Kind == UnsentKind.Call && root.TryGetProperty("channel", out JsonElement value)
                && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            // A recorded Call names its channel as the rep reads it; an older body keeps whatever it held.
            if (Enum.TryParse(channel, out CallChannel recorded)) channel = CallContract.Label(recorded);
            TabletOrder? order = OrderStore.Parse(item);
            if (order is not null && order.Lines.Any(line => line is null || line.Price is null)) order = null;
            return new UnsentView(item, location, order, channel)
            {
                LocationId = locationId,
                MissingProductIds = order?.Lines.Where(line => !productIds.Contains(line.ProductId))
                    .Select(line => line.ProductId).ToHashSet() ?? []
            };
        }).ToArray();
    }
}
