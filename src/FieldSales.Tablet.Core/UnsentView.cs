using System.Text.Json;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

public enum UnsentSection { NeedsAttention, ReadyToSend, InProgress, Other }

/// <summary>The captured work T-01 can name and review, without exposing its raw JSON or server response.</summary>
public sealed record UnsentView(UnsentItem Item, string LocationName, TabletOrder? Order, string? CallChannel)
{
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

    public bool CanMarkReady => Item.Rejection is not null && Item.Kind == UnsentKind.Order
        && Order is { State: OrderState.ReadyToSend, Lines.Count: > 0 };
}

public sealed partial class UnsentStore
{
    public IReadOnlyList<UnsentView> ReadViews()
    {
        if (!File.Exists(workspace.UnsentPath)) return [];
        SnapshotStore? snapshot = File.Exists(workspace.SnapshotPath) ? new(workspace) : null;
        return Read().OrderBy(item => item.SavedUtc).ThenBy(item => item.Id).Select(item =>
        {
            using JsonDocument body = JsonDocument.Parse(item.Json);
            JsonElement root = body.RootElement;
            string location = root.TryGetProperty("locationId", out JsonElement shop) && shop.ValueKind == JsonValueKind.String
                && shop.TryGetGuid(out Guid locationId)
                ? snapshot?.ReadLocation(locationId)?.Location.Name ?? "Shop details unavailable" : "Shop details unavailable";
            string? channel = item.Kind == UnsentKind.Call && root.TryGetProperty("channel", out JsonElement value)
                && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            TabletOrder? order = OrderStore.Parse(item);
            if (order is not null && order.Lines.Any(line => line is null || line.Price is null)) order = null;
            return new UnsentView(item, location, order, channel);
        }).ToArray();
    }
}
