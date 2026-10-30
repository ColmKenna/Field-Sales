using FieldSales.Tablet;
using FieldSales.Tablet.Core;
using FieldSales.Tablet.Components.Layout;
using FieldSales.Tablet.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FieldSales.Tablet.Components.Tests;

public sealed class TabletSessionScreenTests
{
    [Fact]
    public async Task Should_HideTheWorkspaceAndSignOutAction_When_TheSessionEnds()
    {
        await using Screen screen = new(typeof(MainLayout));
        await screen.Open(new() { [nameof(MainLayout.Body)] = (RenderFragment)(b => b.AddContent(0, "Private shop details")) });
        Assert.Contains("Private shop details", screen.Renderer.Text(screen.Id));
        screen.Tablet.Enter = () => Task.FromResult(new TabletEntry(EntryScreen.SignIn, null, "Sign in") { SessionVersion = 1 });
        await screen.Renderer.Dispatcher.InvokeAsync(screen.Tablet.ChangeSession);
        Assert.DoesNotContain("Private shop details", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
        Assert.Contains("/", screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_IgnoreTheOldEntryResult_When_LogoutWinsAnOverlappingGuard()
    {
        TaskCompletionSource<TabletEntry> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using Screen screen = new(typeof(MainLayout));
        screen.Tablet.Enter = () => pending.Task;
        Task opening = screen.Open(new() { [nameof(MainLayout.Body)] = (RenderFragment)(b => b.AddContent(0, "Private shop details")) });
        await screen.Renderer.Dispatcher.InvokeAsync(() =>
        {
            screen.Tablet.Enter = () => Task.FromResult(new TabletEntry(EntryScreen.SignIn, null, "Sign in") { SessionVersion = 1 });
            screen.Tablet.ChangeSession();
        });
        pending.SetResult(new(EntryScreen.Home, "colm", ""));
        await opening;
        Assert.DoesNotContain("Private shop details", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_RequestOnlyTheObservedSessionToEnd_When_TheAppBarSignOutIsUsed()
    {
        await using Screen screen = new(typeof(MainLayout));
        long? requested = null;
        screen.Tablet.End = version =>
        {
            requested = version;
            screen.Tablet.Enter = () => Task.FromResult(new TabletEntry(EntryScreen.SignIn, null, "Sign in") { SessionVersion = 1 });
            screen.Tablet.ChangeSession();
            return Task.FromResult(true);
        };
        await screen.Open(new() { [nameof(MainLayout.Body)] = (RenderFragment)(b => b.AddContent(0, "Private shop details")) });
        await screen.Click("Sign out");
        Assert.Equal(0L, requested);
        Assert.DoesNotContain("Private shop details", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_NotNavigateOrRedisplayData_When_AnOldSyncCompletesAfterSessionChange()
    {
        TaskCompletionSource<SyncOutcome> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.SyncWork = (_, _) => pending.Task;
        await screen.Open(new());
        Task syncing = screen.Click("Sync now");
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.SessionVersion++);
        pending.SetResult(new(SyncStatus.Completed, "Last synced 08:30", null));
        await syncing;
        Assert.Empty(screen.Navigation.Destinations);
        Assert.DoesNotContain("08:30", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_ShowTheUnsentCountWithWhatIsReady_When_WorkIsWaitingOnTheSyncScreen()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadSummary = rep => rep == "colm" ? new UnsentSummary(4, 1, 2) : new UnsentSummary(0, 0, 0);
        await screen.Open(new());
        Assert.Contains("Unsent: 4 · 2 ready to send · 1 in progress", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_SayNothingAboutUnsentWork_When_NothingIsWaitingOnTheSyncScreen()
    {
        await using Screen screen = new(typeof(Sync));
        await screen.Open(new());
        Assert.DoesNotContain("Unsent", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_KeepSyncUsable_When_TheUnsentCountCannotBeRead()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadSummary = _ => throw new IOException("The saved work could not be read.");
        await screen.Open(new());
        Assert.Contains("Sync now", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain("Unsent", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_CancelPageWork_When_TheSyncScreenIsDisposed()
    {
        TaskCompletionSource<SyncOutcome> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using Screen screen = new(typeof(Sync));
        CancellationToken pageWork = default;
        screen.Tablet.SyncWork = (_, ct) => { pageWork = ct; return pending.Task; };
        await screen.Open(new());
        Task syncing = screen.Click("Sync now");
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Renderer.Remove(screen.Id));
        Assert.True(pageWork.IsCancellationRequested);
        pending.SetResult(new(SyncStatus.Completed, "Last synced 08:30", null));
        await syncing;
        Assert.Empty(screen.Navigation.Destinations);
    }

    // WI-039 C1: "Sending 2 of 3" while items go, then the download.
    [Fact]
    public async Task Should_ShowSendingNOfTotal_When_UploadsAreInProgress()
    {
        TaskCompletionSource<SyncOutcome> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.SyncWork = (_, _) => pending.Task;
        await screen.Open(new());
        Task syncing = screen.Click("Sync now");
        await screen.Until(() => screen.Tablet.Progress is not null);

        screen.Tablet.Progress!.Report(new(2, 3));
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Sending 2 of 3…"));
        Assert.DoesNotContain("Downloading your day…", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Sync now", screen.Renderer.Buttons(screen.Id));

        screen.Tablet.Progress.Report(UploadProgress.Finished);
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Downloading your day…"));
        Assert.DoesNotContain("Sending", screen.Renderer.Text(screen.Id));

        pending.SetResult(new(SyncStatus.Completed, "Last synced 07:42", null) { Upload = new(3, 3) });
        await syncing;
        Assert.Equal(["/home"], screen.Navigation.Destinations);
    }

    // WI-039 C2: the rep stays on Sync to see what didn't go; Home only when everything went.
    [Theory]
    [InlineData(SyncStatus.Completed, 3, 1, "2 items not sent. They'll send next Sync.", null)]
    [InlineData(SyncStatus.Completed, 2, 1, "1 item not sent. It'll send next Sync.", null)]
    [InlineData(SyncStatus.NoConnection, 2, 0, "2 items not sent. They'll send next Sync.", SyncMessages.NoConnection)]
    public async Task Should_StayOnSyncWithTheNotSentCount_When_SomeItemsWereNotSent(SyncStatus status, int total, int sent,
        string notSent, string? problem)
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.SyncWork = (_, _) => Task.FromResult(new SyncOutcome(status, status == SyncStatus.Completed ? "Last synced 07:42" : problem!, null)
            { Upload = new(total, sent) });
        await screen.Open(new());

        await screen.Click("Sync now");

        Assert.Empty(screen.Navigation.Destinations);
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains(notSent, text);
        if (problem is null) Assert.DoesNotContain(SyncMessages.NoConnection, text);
        else Assert.Contains(problem, text);
        Assert.Contains("Sync now", screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_GoHome_When_EverythingWasSent()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.SyncWork = (_, _) => Task.FromResult(new SyncOutcome(SyncStatus.Completed, "Last synced 07:42", null) { Upload = new(3, 3) });
        await screen.Open(new());

        await screen.Click("Sync now");

        Assert.Equal(["/home"], screen.Navigation.Destinations);
        Assert.DoesNotContain("not sent", screen.Renderer.Text(screen.Id));
    }

    private sealed class Screen : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        public TabletApp Tablet { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public ScreenRenderer Renderer { get; }
        public int Id { get; }
        public Screen(Type component)
        {
            services = new ServiceCollection().AddLogging().AddSingleton(Tablet)
                .AddSingleton<NavigationManager>(Navigation).AddSingleton<IJSRuntime>(new NoopJS()).BuildServiceProvider();
            Renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            Id = Renderer.Attach(component);
        }
        public Task Open(Dictionary<string, object?> parameters) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Open(Id, parameters));
        public Task Click(string text) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(Id, text));
        public async Task Until(Func<bool> predicate)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (await Renderer.Dispatcher.InvokeAsync(predicate)) return;
                await Task.Delay(10);
            }
            Assert.Fail("The screen never reached the expected state.");
        }
        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync(); await services.DisposeAsync(); Assert.Empty(Renderer.Errors);
        }
    }
    private sealed class TestNavigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/home");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
        protected override void NavigateToCore(string uri, NavigationOptions options) => Destinations.Add(uri);
    }
    private sealed class NoopJS : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class ScreenRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<Exception> Errors { get; } = [];
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public int Attach(Type type) => AssignRootComponentId(InstantiateComponent(type));
        public Task Open(int id, Dictionary<string, object?> parameters) => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
        public void Remove(int id) => RemoveRootComponent(id);
        private RenderTreeFrame[] Frames(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).ToArray();
        }
        public string Text(int id) => string.Concat(Frames(id).Select(frame => frame.FrameType switch
        {
            RenderTreeFrameType.Text => frame.TextContent,
            RenderTreeFrameType.Markup => System.Net.WebUtility.HtmlDecode(frame.MarkupContent),
            RenderTreeFrameType.Component => Text(frame.ComponentId),
            _ => ""
        }));
        public string[] Buttons(int id) => Events(id).Select(e => e.Text).ToArray();
        public Task Click(int id, string text) => DispatchEventAsync(Events(id).First(e => e.Text == text).Event, null, EventArgs.Empty);
        private IEnumerable<(string Text, ulong Event)> Events(int id)
        {
            var frames = Frames(id);
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].FrameType == RenderTreeFrameType.Component)
                    foreach (var child in Events(frames[i].ComponentId)) yield return child;
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != "button") continue;
                var children = frames.Skip(i + 1).Take(frames[i].ElementSubtreeLength - 1).ToArray();
                yield return (string.Concat(children.Where(f => f.FrameType == RenderTreeFrameType.Text).Select(f => f.TextContent)),
                    children.Single(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "onclick").AttributeEventHandlerId);
            }
        }
    }
}
