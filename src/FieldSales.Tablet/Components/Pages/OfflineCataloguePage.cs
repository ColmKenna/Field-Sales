using FieldSales.Tablet.Core;
using Microsoft.AspNetCore.Components;

namespace FieldSales.Tablet.Components.Pages;

/// <summary>Catalogue reads use the current rep and the same 24-hour guard as Home, including after resume.</summary>
public abstract class OfflineCataloguePage : ComponentBase, IDisposable
{
    [Inject] protected TabletApp Tablet { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    protected bool Ready { get; private set; }
    private int load;

    protected override void OnInitialized() => Tablet.Resumed += OnResumed;
    protected override Task OnParametersSetAsync() => RefreshAsync();

    protected async Task RefreshAsync()
    {
        int current = ++load;
        Ready = false;
        Clear();
        StateHasChanged();
        TabletEntry entry = await Tablet.EnterAsync();
        if (current != load) return;
        if (entry.Screen != EntryScreen.Home) { Navigation.NavigateTo("/"); return; }
        await ReadAsync(entry.RepSubject!, () => current == load);
        if (current == load) Ready = true;
    }

    protected abstract void Clear();
    protected abstract Task ReadAsync(string rep, Func<bool> isCurrent);
    private void OnResumed() => _ = InvokeAsync(async () => { await RefreshAsync(); StateHasChanged(); });
    public void Dispose() { ++load; Tablet.Resumed -= OnResumed; }
}
