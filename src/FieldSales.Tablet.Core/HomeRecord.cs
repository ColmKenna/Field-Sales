using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

public sealed record HomeCounter(string Id, string Label, int Count, string Href, string? Detail = null);
public sealed record HomeCall(Guid Id, DateTimeOffset SavedAt, string Channel);
public sealed record HomeOrder(Guid Id, DateTimeOffset SavedAt, OrderStanding Standing, int Lines)
{
    public string State => Standing switch
    {
        OrderStanding.InProgress => "In Progress",
        OrderStanding.ReadyToSend => "Ready to Send",
        OrderStanding.Sending => "Sending",
        OrderStanding.NeedsAttention => "Needs Attention",
        OrderStanding.Sent => "Sent",
        _ => "Unavailable"
    };
}
public sealed record HomeDayLocation(HomeLocation Location, bool IsPlanned, IReadOnlyList<HomeCall> Calls, IReadOnlyList<HomeOrder> Orders)
{
    public bool Done => Calls.Count > 0;
}
public sealed record HomeDaySection(string Id, string Title, IReadOnlyList<HomeDayLocation> Locations, string? EmptyText = null);
public sealed record HomeDayRecord(DateOnly Day, IReadOnlyList<HomeCounter> Counters, IReadOnlyList<HomeDaySection> Sections, bool HasUnreadableActivity = false);

/// <summary>Today's local record, including confirmed work. No network, capture changes or visit planning.</summary>
public sealed class HomeRecordReader(RepWorkspace workspace, TimeProvider clock)
{
    /// <param name="planned">An ordered, already-resolved plan supplied by future visit planning.
    /// Production currently supplies none; checkpoint fixtures exercise this composition boundary.</param>
    public HomeDayRecord Read(IReadOnlyList<HomeLocation>? planned = null)
    {
        planned ??= [];
        DateOnly today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        IReadOnlyList<HomeStoredWork> work = new UnsentStore(workspace).ReadHomeWork();
        int total = work.Count(row => row.SentUtc is null);
        int attention = work.Count(row => row.SentUtc is null && row.Item.Rejection is not null);
        HomeCounter[] counters = [new("unsent", "Unsent", total, "/sync", attention == 0 ? null
            : attention == 1 ? "1 needs attention" : $"{attention} need attention")];
        SnapshotStore? snapshot = File.Exists(workspace.SnapshotPath) ? new(workspace, clock) : null;
        Dictionary<Guid, HomeLocation> identities = snapshot?.ReadLocations().ToDictionary(row => row.Id) ?? [];
        foreach (HomeLocation location in planned) identities.TryAdd(location.Id, location);
        Dictionary<Guid, List<HomeCall>> calls = [];
        Dictionary<Guid, List<HomeOrder>> orders = [];
        bool unreadable = false;

        foreach (HomeStoredWork row in work)
        {
            if (row.Item.Kind == UnsentKind.Call)
            {
                var call = CallStore.Parse(row.Item);
                if (call is null || call.Version != CallContract.CurrentVersion || call.Id != row.Item.Id
                    || call.CapturingRep != workspace.RepSubject || call.LocationId == Guid.Empty
                    || call.CalledOn == default || call.SavedAt == default || !Enum.IsDefined(call.Channel))
                { unreadable = true; continue; }
                if (call.CalledOn != today) continue;
                if (!calls.TryGetValue(call.LocationId, out var entries)) calls[call.LocationId] = entries = [];
                entries.Add(new(call.Id, TimeZoneInfo.ConvertTime(call.SavedAt, clock.LocalTimeZone), CallContract.Label(call.Channel)));
            }
            else if (row.Item.Kind == UnsentKind.Order)
            {
                var order = OrderStore.Parse(row.Item);
                if (order is null || order.Version != OrderContract.CurrentVersion || order.Id != row.Item.Id
                    || order.CapturingRep != workspace.RepSubject || order.LocationId == Guid.Empty
                    || order.TakenOn == default || order.Lines is null || !Enum.IsDefined(order.State))
                { unreadable = true; continue; }
                if (order.TakenOn != today) continue;
                OrderStanding state = row.SentUtc is not null ? OrderStanding.Sent
                    : row.Claimed ? OrderStanding.Sending
                    : row.Item.Rejection is not null ? OrderStanding.NeedsAttention
                    : order.State == OrderState.ReadyToSend ? OrderStanding.ReadyToSend : OrderStanding.InProgress;
                if (!orders.TryGetValue(order.LocationId, out var entries)) orders[order.LocationId] = entries = [];
                entries.Add(new(order.Id, TimeZoneInfo.ConvertTime(row.Item.SavedUtc, clock.LocalTimeZone), state, order.Lines.Count));
            }
        }

        HomeDayLocation Row(Guid id, bool isPlanned)
        {
            if (!identities.TryGetValue(id, out HomeLocation? location))
            {
                var retained = snapshot?.ReadLocation(id)?.Location;
                location = retained is null ? new(id, "Shop details unavailable", "", "")
                    : new(id, retained.Name, retained.Town.Name, retained.CustomerName);
                identities[id] = location;
            }
            return new(location, isPlanned,
                calls.GetValueOrDefault(id, []).OrderBy(call => call.SavedAt).ThenBy(call => call.Id).ToArray(),
                orders.GetValueOrDefault(id, []).OrderBy(order => order.SavedAt).ThenBy(order => order.Id).ToArray());
        }
        HashSet<Guid> plannedIds = planned.Select(location => location.Id).ToHashSet();
        HomeDayLocation[] plannedRows = planned.DistinctBy(location => location.Id).Select(location => Row(location.Id, true)).ToArray();
        HomeDayLocation[] unplanned = calls.Keys.Concat(orders.Keys).Distinct().Where(id => !plannedIds.Contains(id))
            .Select(id => Row(id, false)).OrderBy(row => row.Location.TownName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Location.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Location.Id).ToArray();
        return new(today, counters,
            [new("today", "Today", plannedRows, "No visits scheduled today"),
             new("unplanned", "Unplanned today", unplanned, "No unplanned activity today")], unreadable);
    }
}
