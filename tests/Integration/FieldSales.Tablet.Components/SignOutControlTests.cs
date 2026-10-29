using FieldSales.Tablet.Components.Layout;
using FieldSales.Tablet.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FieldSales.Tablet.Components.Tests;

public sealed class SignOutControlTests
{
    [Fact]
    public async Task Should_WarnWithTheCountAndTypes_When_ThreeItemsAreUnsent()
    {
        await using var screen = new Screen();
        await screen.Open();
        await screen.Click("Sign out");

        Assert.Contains("3 items have not synced: 2 Calls and 1 Order.", screen.Renderer.Text(screen.Id));
        Assert.Contains("They will stay on this tablet for you after sign-out.", screen.Renderer.Text(screen.Id));
        Assert.Equal(["Sign out", "Stay signed in", "Sign out", "Open Sync & unsent"], screen.Renderer.Buttons(screen.Id));
        Assert.True(screen.JS.IsOpen);
        Assert.Equal(0, screen.SignOuts);
        Assert.Empty(screen.Navigation.Destinations);
        Assert.Single(screen.Renderer.Attributes(screen.Id, "aria-labelledby"));
        Assert.Single(screen.Renderer.Attributes(screen.Id, "aria-describedby"));
    }

    [Fact]
    public async Task Should_StayOnTheCurrentScreen_When_StaySignedInIsChosen()
    {
        await using var screen = new Screen();
        await screen.Open();
        await screen.Click("Sign out");
        await screen.Click("Stay signed in");

        Assert.False(screen.JS.IsOpen);
        Assert.Equal(["tabletSignOut.open", "tabletSignOut.close"], screen.JS.Calls);
        Assert.Equal(0, screen.SignOuts);
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_RequestSignOutOnlyAfterClosingTheWarning_When_Confirmed()
    {
        await using var screen = new Screen();
        await screen.Open();
        await screen.Click("Sign out");
        await screen.Click("Sign out", occurrence: 1);

        Assert.False(screen.JS.IsOpen);
        Assert.Equal(1, screen.SignOuts);
        Assert.False(screen.WasOpenAtSignOut);
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_OpenSyncWithoutRequestingSignOut_When_OpenSyncAndUnsentIsChosen()
    {
        await using var screen = new Screen();
        await screen.Open();
        await screen.Click("Sign out");
        await screen.Click("Open Sync & unsent");

        Assert.False(screen.JS.IsOpen);
        Assert.Equal(["/sync"], screen.Navigation.Destinations);
        Assert.Equal(0, screen.SignOuts);
    }

    [Fact]
    public async Task Should_BypassTheWarning_When_NoItemsAreUnsent()
    {
        await using var screen = new Screen { Read = () => Task.FromResult<IReadOnlyList<UnsentKind>>([]) };
        await screen.Open();
        await screen.Click("Sign out");

        Assert.Equal(1, screen.SignOuts);
        Assert.Empty(screen.JS.Calls);
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Theory]
    [InlineData(UnsentKind.Call, "1 item has not synced: 1 Call.")]
    [InlineData(UnsentKind.Order, "1 item has not synced: 1 Order.")]
    [InlineData(UnsentKind.ScheduleChange, "1 item has not synced: 1 Schedule change.")]
    public async Task Should_DescribeASingleItem_When_OnlyOneKindIsWaiting(UnsentKind kind, string expected)
    {
        await using var screen = new Screen { Read = () => Task.FromResult<IReadOnlyList<UnsentKind>>([kind]) };
        await screen.Open();
        await screen.Click("Sign out");
        Assert.Contains(expected, screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_RecountWithoutChangingEitherWorkspace_When_TheWarningIsReopened()
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldsales-sign-out-dialog", Guid.NewGuid().ToString("N"));
        try
        {
            UnsentStore colm = new(new RepWorkspace(root, "colm"));
            UnsentStore aoife = new(new RepWorkspace(root, "aoife"));
            colm.Save(new(Guid.NewGuid(), UnsentKind.Call, DateTimeOffset.UtcNow, """{"state":"Saved"}"""));
            aoife.Save(new(Guid.NewGuid(), UnsentKind.Order, DateTimeOffset.UtcNow, """{"state":"Ready to Send"}"""));
            string otherWork = aoife.Fingerprint();
            await using var screen = new Screen { Read = () => Task.FromResult<IReadOnlyList<UnsentKind>>(colm.Read().Select(item => item.Kind).ToArray()) };
            await screen.Open();
            await screen.Click("Sign out");
            Assert.Contains("1 item has not synced: 1 Call.", screen.Renderer.Text(screen.Id));
            await screen.Click("Stay signed in");
            colm.Save(new(Guid.NewGuid(), UnsentKind.Order, DateTimeOffset.UtcNow, """{"state":"Ready to Send"}"""));
            string currentWork = colm.Fingerprint();
            await screen.Click("Sign out");
            Assert.Contains("2 items have not synced: 1 Call and 1 Order.", screen.Renderer.Text(screen.Id));
            await screen.Click("Open Sync & unsent");
            Assert.Equal(currentWork, colm.Fingerprint());
            Assert.Equal(otherWork, aoife.Fingerprint());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Should_KeepAccessAndAllowRetry_When_CountingFails()
    {
        await using var screen = new Screen { Read = () => throw new IOException("Test read failure") };
        await screen.Open();
        await screen.Click("Sign out");
        Assert.Contains("Couldn't sign out.", screen.Renderer.Text(screen.Id));
        Assert.Equal(0, screen.SignOuts);
        Assert.Empty(screen.JS.Calls);
        screen.Read = () => Task.FromResult<IReadOnlyList<UnsentKind>>([]);
        await screen.Click("Sign out");
        Assert.Equal(1, screen.SignOuts);
    }

    [Fact]
    public async Task Should_IgnoreALateCount_When_TheControlHasBeenDisposed()
    {
        TaskCompletionSource<IReadOnlyList<UnsentKind>> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var screen = new Screen { Read = () => pending.Task };
        await screen.Open();
        Task opening = screen.Click("Sign out");
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Renderer.DisposeControl(screen.Id));
        pending.SetResult([]);
        await opening;
        Assert.Equal(0, screen.SignOuts);
        Assert.Empty(screen.JS.Calls);
    }

    private sealed class Screen : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        public Func<Task<IReadOnlyList<UnsentKind>>> Read { get; set; } =
            () => Task.FromResult<IReadOnlyList<UnsentKind>>([UnsentKind.Call, UnsentKind.Call, UnsentKind.Order]);
        public DialogJS JS { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public DialogRenderer Renderer { get; }
        public int Id { get; }
        public int SignOuts { get; private set; }
        public bool WasOpenAtSignOut { get; private set; }

        public Screen()
        {
            services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(JS)
                .AddSingleton<NavigationManager>(Navigation).BuildServiceProvider();
            Renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            Id = Renderer.Attach();
        }
        public Task Open() => Renderer.Dispatcher.InvokeAsync(() => Renderer.Open(Id, new()
        {
            [nameof(SignOutControl.ReadUnsentKinds)] = (Func<Task<IReadOnlyList<UnsentKind>>>)(() => Read()),
            [nameof(SignOutControl.SignOutRequested)] = EventCallback.Factory.Create(this, () =>
            {
                SignOuts++;
                WasOpenAtSignOut = JS.IsOpen;
            })
        }));
        public Task Click(string label, int occurrence = 0) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(Id, label, occurrence));
        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await services.DisposeAsync();
            Assert.Empty(Renderer.Errors);
        }
    }

    private sealed class DialogJS : IJSRuntime
    {
        public List<string> Calls { get; } = [];
        public bool IsOpen { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            Calls.Add(identifier);
            IsOpen = identifier == "tabletSignOut.open";
            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/home");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
    }

    private sealed class DialogRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<Exception> Errors { get; } = [];
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public int Attach() => AssignRootComponentId(InstantiateComponent(typeof(SignOutControl)));
        public Task Open(int id, Dictionary<string, object?> parameters) => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
        public void DisposeControl(int id) => RemoveRootComponent(id);
        private RenderTreeFrame[] Frames(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).ToArray();
        }
        public string Text(int id) => System.Net.WebUtility.HtmlDecode(string.Concat(Frames(id)
            .Where(f => f.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
            .Select(f => f.FrameType == RenderTreeFrameType.Text ? f.TextContent : f.MarkupContent)));
        public string[] Attributes(int id, string name) => Frames(id)
            .Where(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == name)
            .Select(f => f.AttributeValue?.ToString() ?? "").ToArray();
        public string[] Buttons(int id) => ButtonEvents(id).Select(b => b.Text).ToArray();
        public Task Click(int id, string label, int occurrence) => DispatchEventAsync(
            ButtonEvents(id).Where(b => b.Text == label).ElementAt(occurrence).Event, null, EventArgs.Empty);
        private IEnumerable<(string Text, ulong Event)> ButtonEvents(int id)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != "button") continue;
                RenderTreeFrame[] children = frames.Skip(i + 1).Take(frames[i].ElementSubtreeLength - 1).ToArray();
                yield return (System.Net.WebUtility.HtmlDecode(string.Concat(children
                        .Where(f => f.FrameType == RenderTreeFrameType.Text).Select(f => f.TextContent))),
                    children.Single(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "onclick").AttributeEventHandlerId);
            }
        }
    }
}
