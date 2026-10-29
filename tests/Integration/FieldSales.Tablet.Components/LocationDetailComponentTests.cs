using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet;
using FieldSales.Tablet.Core;
using FieldSales.Tablet.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSales.Tablet.Components.Tests;

public sealed class LocationDetailComponentTests
{
    private static readonly Guid ShopId = Guid.Parse("55000000-0000-0000-0000-000000000001");
    private static OfflineLocationDetail Shop(bool assigned = true, bool contact = true) => new(
        new SnapshotLocation(ShopId, "Doyle's Shop", Guid.NewGuid(), "Doyle Retail",
            new(Guid.NewGuid(), "Arklow", "Wicklow"), "Y14 X2F9", contact
                ? new(Guid.NewGuid(), "Mary Hickey", "Owner", "+353 402 31234", "mary@example.test") : null), assigned, !assigned);

    [Fact]
    public async Task Should_ShowAddressContactAndEqualActions_When_AssignedShopIsOpenedOffline()
    {
        await using var screen = new Screen((_, _) => Shop());
        await screen.Open(ShopId);
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Doyle", text);
        Assert.Contains("Arklow", text);
        Assert.Contains("Wicklow", text);
        Assert.Contains("Y14 X2F9", text);
        Assert.Contains("Mary Hickey", text);
        Assert.Contains("Main contact: Mary Hickey", text);
        Assert.Contains("+353 402 31234", text);
        Assert.Contains("mary@example.test", text);
        Assert.Equal(new[] { "Record Call", "New Order" }, screen.Renderer.Buttons(screen.Id));
        Assert.Equal(2, screen.Renderer.AttributeValues(screen.Id, "class").Count(value => value == "primary"));
        Assert.Contains("shop-actions", screen.Renderer.AttributeValues(screen.Id, "class"));
    }

    [Fact]
    public async Task Should_ShowInactiveMainContactInIdentityBlock_When_SnapshotContainsReplacementGap()
    {
        OfflineLocationDetail shop = Shop();
        shop = shop with { Location = shop.Location with { MainContact = null,
            MainContactReplacement = new(Guid.NewGuid(), "Mary Walsh", true) } };
        await using var screen = new Screen((_, _) => shop);
        await screen.Open(ShopId);
        Assert.Contains("Main contact: Mary Walsh (inactive) — replacement needed", screen.Renderer.TextWithinClass(screen.Id, "shop-identity"));
        Assert.DoesNotContain("No main contact in your downloaded data.", screen.Renderer.Text(screen.Id));
        Assert.Equal(new[] { "Record Call", "New Order" }, screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_KeepReplacementStateWithNoActions_When_AnUnsentOrderRetainsTheShop()
    {
        OfflineLocationDetail shop = Shop(assigned: false);
        shop = shop with { Location = shop.Location with { MainContact = null,
            MainContactReplacement = new(Guid.NewGuid(), "Mary Walsh", true) } };
        await using var screen = new Screen((_, _) => shop);
        await screen.Open(ShopId);
        Assert.Contains("Main contact: Mary Walsh (inactive) — replacement needed", screen.Renderer.TextWithinClass(screen.Id, "shop-identity"));
        Assert.Contains("No longer assigned to you — your existing order will still be sent", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_KeepGapWithoutInactiveWording_When_OutgoingContactWasReactivated()
    {
        OfflineLocationDetail shop = Shop();
        shop = shop with { Location = shop.Location with { MainContact = null,
            MainContactReplacement = new(Guid.NewGuid(), "Mary Walsh", false) } };
        await using var screen = new Screen((_, _) => shop);
        await screen.Open(ShopId);
        string identity = screen.Renderer.TextWithinClass(screen.Id, "shop-identity");
        Assert.Contains("Main contact replacement needed", identity);
        Assert.Contains("Mary Walsh", identity);
        Assert.DoesNotContain("(inactive)", identity);
        Assert.DoesNotContain("Main contact: Mary Walsh", identity);
    }

    [Fact]
    public async Task Should_ClearReplacementWording_When_RefreshedSnapshotSelectsActiveMain()
    {
        OfflineLocationDetail shop = Shop();
        shop = shop with { Location = shop.Location with { MainContact = null,
            MainContactReplacement = new(Guid.NewGuid(), "Mary Walsh", true) } };
        await using var screen = new Screen((_, _) => shop);
        await screen.Open(ShopId);
        Assert.Contains("replacement needed", screen.Renderer.Text(screen.Id));
        shop = Shop();
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.Resume());
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Main contact: Mary Hickey"));
        Assert.DoesNotContain("replacement needed", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Mary Walsh", screen.Renderer.Text(screen.Id));
        Assert.Equal(2, screen.Renderer.Buttons(screen.Id).Count);
    }

    [Fact]
    public async Task Should_ShowMissingContactState_When_NoMainContactIsDownloaded()
    {
        await using var screen = new Screen((_, _) => Shop(contact: false));
        await screen.Open(ShopId);
        Assert.Contains("No main contact in your downloaded data.", screen.Renderer.Text(screen.Id));
        Assert.Equal(2, screen.Renderer.Buttons(screen.Id).Count);
    }

    [Fact]
    public async Task Should_ShowHistoryUnavailable_When_NoHistorySourceExists()
    {
        await using var screen = new Screen((_, _) => Shop());
        await screen.Open(ShopId);
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Recent calls aren't available on this tablet yet.", text);
        Assert.Contains("Recent orders aren't available on this tablet yet.", text);
        Assert.DoesNotContain("/calls/", string.Join(" ", screen.Renderer.AttributeValues(screen.Id, "href")));
        Assert.DoesNotContain("/orders/", string.Join(" ", screen.Renderer.AttributeValues(screen.Id, "href")));
    }

    [Theory]
    [InlineData("Record Call")]
    [InlineData("New Order")]
    public async Task Should_ExplainTemporaryUnavailability_When_EitherActionIsTapped(string action)
    {
        await using var screen = new Screen((_, _) => Shop());
        await screen.Open(ShopId);
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Renderer.Click(screen.Id, action));
        Assert.Contains($"{action} isn't available on this tablet yet.", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_ShowReassignmentNoticeAndHideActions_When_AnUnsentOrderReferencesTheShop()
    {
        await using var screen = new Screen((_, _) => Shop(assigned: false));
        await screen.Open(ShopId);
        Assert.Contains("No longer assigned to you — your existing order will still be sent", screen.Renderer.Text(screen.Id));
        Assert.Contains("assignment-notice", screen.Renderer.AttributeValues(screen.Id, "class"));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_ClearShopData_When_AccessExpiresRepChangesOrNavigationOverlaps()
    {
        OfflineLocationDetail colm = Shop();
        await using var screen = new Screen((rep, _) => rep == "colm" ? colm : null);
        await screen.Open(ShopId);
        var entry = new TaskCompletionSource<TabletEntry>();
        screen.Tablet.Enter = () => entry.Task;
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.Resume());
        Assert.DoesNotContain("Doyle", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
        entry.SetResult(new(EntryScreen.ConnectToSignInAgain, null, "Connect"));
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);
        Assert.Equal("/", screen.Navigation.Destinations.Last());

        screen.Tablet.Enter = () => Task.FromResult(new TabletEntry(EntryScreen.Home, "aoife", ""));
        await screen.Open(ShopId);
        Assert.DoesNotContain("Doyle", screen.Renderer.Text(screen.Id));
        Assert.Contains("Shop unavailable", screen.Renderer.Text(screen.Id));

        // An old route's worker read must not repopulate the screen after a new Location opens.
        screen.Tablet.Enter = () => Task.FromResult(new TabletEntry(EntryScreen.Home, "colm", ""));
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource();
        screen.Tablet.Read = (_, id) =>
        {
            if (id == ShopId)
            {
                started.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Old read was not released.");
                return colm;
            }
            return null;
        };
        Task old = screen.Open(ShopId);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task newer = screen.Open(Guid.NewGuid());
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Shop unavailable"));
        release.Set();
        await Task.WhenAll(old, newer);
        Assert.DoesNotContain("Doyle", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_NotPublishTheOldShop_When_TheSessionChangesDuringAWorkerRead()
    {
        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var screen = new Screen((_, _) =>
        {
            started.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Read was not released.");
            return Shop();
        });
        Task opening = screen.Open(ShopId);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.SessionVersion++);
        release.Set();
        await opening;
        Assert.DoesNotContain("Doyle", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    private sealed class Screen : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        public TabletApp Tablet { get; }
        public TestNavigation Navigation { get; } = new();
        public ComponentRenderer Renderer { get; }
        public int Id { get; }

        public Screen(Func<string, Guid, OfflineLocationDetail?> read)
        {
            Tablet = new() { Read = read };
            services = new ServiceCollection().AddLogging().AddSingleton(Tablet)
                .AddSingleton<NavigationManager>(Navigation).BuildServiceProvider();
            Renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            Id = Renderer.Attach();
        }

        public Task Open(Guid id) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Open(Id, id));
        public async Task Until(Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await Renderer.Dispatcher.InvokeAsync(predicate)) await Task.Delay(10, timeout.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await services.DisposeAsync();
            Assert.Empty(Renderer.Errors);
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/locations/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
    }

    private sealed class ComponentRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<Exception> Errors { get; } = [];
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public int Attach() => AssignRootComponentId(InstantiateComponent(typeof(LocationDetail)));
        public Task Open(int id, Guid location) => RenderRootComponentAsync(id,
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(LocationDetail.Id)] = location }));
        private RenderTreeFrame[] Frames(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).ToArray();
        }
        public string Text(int id) => System.Net.WebUtility.HtmlDecode(string.Concat(Frames(id)
            .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
            .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent)));
        public string TextWithinClass(int id, string name)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element) continue;
                RenderTreeFrame[] children = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1).ToArray();
                if (!children.TakeWhile(frame => frame.FrameType == RenderTreeFrameType.Attribute)
                    .Any(frame => frame.AttributeName == "class" && frame.AttributeValue?.ToString() == name)) continue;
                return System.Net.WebUtility.HtmlDecode(string.Concat(children
                    .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
                    .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent)));
            }
            throw new InvalidOperationException($"Rendered element .{name} was not found.");
        }
        public IReadOnlyList<string> AttributeValues(int id, string name) => Frames(id)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == name)
            .Select(frame => frame.AttributeValue?.ToString() ?? "").ToArray();
        public IReadOnlyList<string> Buttons(int id) => ButtonEvents(id).Select(button => button.Text).ToArray();
        public Task Click(int id, string text) => DispatchEventAsync(ButtonEvents(id).Single(button => button.Text == text).Event,
            null, EventArgs.Empty);
        private IEnumerable<(string Text, ulong Event)> ButtonEvents(int id)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element || frames[index].ElementName != "button") continue;
                RenderTreeFrame[] button = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1).ToArray();
                yield return (string.Concat(button.Where(frame => frame.FrameType == RenderTreeFrameType.Text).Select(frame => frame.TextContent)),
                    button.Single(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick").AttributeEventHandlerId);
            }
        }
    }
}
