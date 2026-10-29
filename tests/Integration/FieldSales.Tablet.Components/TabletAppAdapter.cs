using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>Desktop adapter for the MAUI boundary only. The linked component and guard are
/// production source; storage/session rules are covered in Tablet.Core integration tests.</summary>
public sealed class TabletApp
{
    public event Action? Resumed;
    public void Resume() => Resumed?.Invoke();
    public Func<Task<TabletEntry>> Enter { get; set; } = () => Task.FromResult(new TabletEntry(EntryScreen.Home, "colm", ""));
    public Func<string, Guid, OfflineLocationDetail?> Read { get; set; } = (_, _) => null;
    public Task<TabletEntry> EnterAsync() => Enter();
    public OfflineLocationDetail? ReadLocation(string rep, Guid id) => Read(rep, id);
}
