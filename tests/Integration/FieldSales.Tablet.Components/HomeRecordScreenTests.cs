using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

public sealed partial class TabletSessionScreenTests
{
    [Fact]
    public async Task Should_KeepSearchProductsSyncAndDownloadedMetadata_When_TheDayRecordCannotBeRead()
    {
        await using Screen screen = new(typeof(Home));
        screen.Tablet.ReadDay = _ => throw new IOException("Private disk diagnostics");
        await screen.Open(new());
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Last synced 07:42", text);
        Assert.Contains("Search shops", text);
        Assert.Contains("Products", text);
        Assert.Contains("Sync", text);
        Assert.Contains("Your saved work is still on this tablet", text);
        Assert.DoesNotContain("Private disk", text);
    }

    [Fact]
    public async Task Should_NotPublishTheOldDay_When_TheSessionChangesDuringTheRead()
    {
        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using Screen screen = new(typeof(Home));
        screen.Tablet.ReadDay = _ =>
        {
            started.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Day read wasn't released.");
            return new(new(2026, 9, 22), [new("unsent", "Private counter", 3, "/sync")], []);
        };
        Task opening = screen.Open(new());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.SessionVersion++);
        release.Set();
        await opening;
        Assert.DoesNotContain("Private counter", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Last synced", screen.Renderer.Text(screen.Id));
    }
}
