using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-038 scenarios 14-18: New Order on the tablet, from search to a saved In Progress order.</summary>
public sealed class NewOrderComponentTests
{
    private static readonly Guid ShopId = Guid.Parse("55000000-0000-0000-0000-000000000001");
    private static readonly OrderSnapshotRef Snapshot = new("1.2", new DateTimeOffset(2026, 10, 29, 6, 30, 0, TimeSpan.Zero));

    private static readonly ProductSearchResult[] Catalogue =
    [
        new(Guid.Parse("66000000-0000-0000-0000-000000000001"), "VITD-1000-90", "Vitamin D 1000IU 90s", "Health > Vitamins", "Each", 12.50m),
        new(Guid.Parse("66000000-0000-0000-0000-000000000002"), "TEA-LOOSE", "Loose Herbal Tea", "Grocery > Tea", "kg", 4.80m),
        new(Guid.Parse("66000000-0000-0000-0000-000000000003"), "VITC-500", "Vitamin C 500mg", "Health > Vitamins", "Each", null),
    ];

    private static OfflineLocationDetail Shop(bool assigned = true) => new(
        new SnapshotLocation(ShopId, "Byrne's Chemist", Guid.NewGuid(), "Byrne Retail", new(Guid.NewGuid(), "Arklow", "Wicklow"), null, null),
        assigned, !assigned);

    /// <summary>Stands in for OrderStore: keeps the order the page has built so far.</summary>
    private sealed class FakeOrders
    {
        public List<(Guid Order, Guid Shop, Guid Product, decimal Quantity)> Calls { get; } = [];
        public TabletOrder? Order { get; private set; }
        public string? Refuse { get; set; }
        public ManualResetEventSlim? Gate { get; set; }
        public TaskCompletionSource? Started { get; set; }

        public OrderResult Add(string rep, Guid orderId, Guid shop, Guid product, decimal quantity)
        {
            Calls.Add((orderId, shop, product, quantity));
            Started?.TrySetResult();
            if (Gate is not null && !Gate.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The save was not released.");
            if (Refuse is not null) return new(null, Refuse);
            ProductSearchResult found = Catalogue.Single(candidate => candidate.Id == product);
            OrderLine line = new(Guid.NewGuid(), found.Id, found.Code, found.Name, quantity, found.Unit,
                new(found.Price!.Value, "Base", "Base price", [new("Base", "Base price", found.Price.Value, true)]), Snapshot);
            Order = (Order ?? new TabletOrder(OrderContract.Schema, OrderContract.CurrentVersion, orderId, OrderState.InProgress, shop, shop,
                rep, new DateOnly(2026, 10, 29), null, Snapshot, [])) with { Lines = [.. Order?.Lines ?? [], line] };
            return new(Order, null);
        }

        public TabletOrder? Find(string rep, Guid orderId) => Order is { } order && order.Id == orderId ? order : null;
    }

    private static Screen Open(FakeOrders orders, bool assigned = true)
    {
        Screen screen = new(orders, assigned);
        return screen;
    }

    [Fact]
    public async Task Should_ShowTheShopASearchBoxAndAnEmptyOrder_When_NewOrderOpens()
    {
        await using Screen screen = Open(new());
        await screen.Show();

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("New order", text);
        Assert.Contains("Byrne's Chemist", text);
        Assert.Contains("No products yet.", text);
        Assert.Contains("0 lines · €0.00", text);
        Assert.Contains("Save and close", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain("Add to order", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("product-search", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Contains($"/locations/{ShopId}", screen.Renderer.AttributeValues(screen.Id, "href"));
    }

    [Fact]
    public async Task Should_ListMatchesWithBreadcrumbAndPrice_When_TheRepTypes()
    {
        await using Screen screen = Open(new());
        await screen.Show();

        await screen.Type("product-search", "vitamin");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("2 matching products"));

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Vitamin D 1000IU 90s", text);
        Assert.Contains("Health > Vitamins", text);
        Assert.Contains("VITD-1000-90", text);
        Assert.Contains("€12.50", text);
        Assert.Contains("No price yet", text);
        await screen.Type("product-search", "no such thing");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("No products match"));
    }

    [Fact]
    public async Task Should_SaveTheLinesOfOneOrderAndShowTheFooter_When_ProductsAreSelectedAndAdded()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();

        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");
        Assert.Equal("", screen.Renderer.InputValue(screen.Id, "line-quantity")); // The quantity never opens prefilled.
        await screen.Type("line-quantity", "12");
        await screen.Click("Add to order");
        await screen.Until(() => orders.Calls.Count == 1 && screen.Renderer.Text(screen.Id).Contains("1 line · €150.00"));

        await screen.Type("product-search", "tea");
        await screen.Pick("Loose Herbal Tea");
        await screen.Type("line-quantity", "1.5");
        await screen.Click("Add to order");
        await screen.Until(() => orders.Calls.Count == 2 && screen.Renderer.Text(screen.Id).Contains("2 lines · €157.20"));

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Vitamin D 1000IU 90s", text);
        Assert.Contains("1.5 kg", text);
        Assert.DoesNotContain("No products yet.", text);
        Assert.Equal([(Catalogue[0].Id, 12m), (Catalogue[1].Id, 1.5m)], orders.Calls.Select(call => (call.Product, call.Quantity)));
        Assert.Single(orders.Calls.Select(call => call.Order).Distinct()); // One order for the whole screen.
        Assert.All(orders.Calls, call => Assert.Equal(ShopId, call.Shop));
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_ShowTheStoreMessageAndKeepTheProduct_When_AQuantityIsRefused()
    {
        FakeOrders orders = new() { Refuse = "Enter a quantity of 1 or more" };
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        await screen.Type("line-quantity", "0");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Enter a quantity of 1 or more"));

        Assert.Contains("alert", screen.Renderer.AttributeValues(screen.Id, "role"));
        Assert.Contains("Add to order", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("No products yet.", screen.Renderer.Text(screen.Id));
        Assert.Equal(0m, Assert.Single(orders.Calls).Quantity);
    }

    [Fact]
    public async Task Should_TreatTextThatIsNotANumberAsNoQuantity_When_AddIsTapped()
    {
        FakeOrders orders = new() { Refuse = "Enter a quantity of 1 or more" };
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        await screen.Type("line-quantity", "twelve");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Enter a quantity of 1 or more"));

        Assert.Equal(0m, Assert.Single(orders.Calls).Quantity);
    }

    [Fact]
    public async Task Should_NotOfferToAdd_When_TheProductHasNoPrice()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin c");
        await screen.Pick("Vitamin C 500mg");

        Assert.Contains("No price yet — this product can't be ordered.", screen.Renderer.Text(screen.Id));
        Assert.Contains("disabled", screen.Renderer.AttributeNamesOnButton(screen.Id, "Add to order"));
        Assert.Empty(orders.Calls);
    }

    [Fact]
    public async Task Should_AddOnce_When_AddIsTappedTwiceWhileTheFirstIsSaving()
    {
        using ManualResetEventSlim release = new();
        FakeOrders orders = new() { Gate = release, Started = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");
        await screen.Type("line-quantity", "12");

        Task first = screen.Click("Add to order");
        await orders.Started!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task second = screen.Click("Add to order");
        release.Set();
        await Task.WhenAll(first, second);
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line"));

        Assert.Single(orders.Calls); // The second tap was ignored while the first was saving.
        Assert.Equal(12m, Assert.Single(orders.Order!.Lines).Quantity);
    }

    [Fact]
    public async Task Should_ReturnToTheShop_When_SaveAndCloseIsTapped()
    {
        await using Screen screen = Open(new());
        await screen.Show();

        await screen.Click("Save and close");

        Assert.Equal([$"/locations/{ShopId}"], screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_RestoreTheSavedLines_When_TheScreenResumes()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");
        await screen.Type("line-quantity", "12");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €150.00"));

        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.Resume());
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €150.00"));

        Assert.Contains("Vitamin D 1000IU 90s", screen.Renderer.Text(screen.Id));
        await screen.Type("product-search", "tea");
        await screen.Pick("Loose Herbal Tea");
        await screen.Type("line-quantity", "1");
        await screen.Click("Add to order");
        await screen.Until(() => orders.Calls.Count == 2);
        Assert.Single(orders.Calls.Select(call => call.Order).Distinct()); // Resuming keeps the same order.
    }

    [Fact]
    public async Task Should_ExplainTheShopIsNotAssignedAndOfferNoSearch_When_TheShopIsNoLongerAssigned()
    {
        await using Screen screen = Open(new(), assigned: false);
        await screen.Show();

        Assert.Contains("This shop isn't assigned to you.", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("product-search", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Empty(screen.Renderer.Buttons(screen.Id));
    }

    private sealed class Screen : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        public TabletApp Tablet { get; }
        public TestNavigation Navigation { get; } = new();
        public ComponentRenderer Renderer { get; }
        public int Id { get; }

        public Screen(FakeOrders orders, bool assigned)
        {
            Tablet = new()
            {
                Read = (_, _) => Shop(assigned),
                ReadIndex = _ => new ProductSearchIndex("generation", Catalogue),
                AddWork = orders.Add,
                FindOrder = orders.Find
            };
            services = new ServiceCollection().AddLogging().AddSingleton(Tablet).AddSingleton<NavigationManager>(Navigation).BuildServiceProvider();
            Renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            Id = Renderer.Attach();
        }

        public Task Show() => Renderer.Dispatcher.InvokeAsync(() => Renderer.Open(Id, ShopId));
        public Task Type(string inputId, string text) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Type(Id, inputId, text));
        public Task Click(string text) => Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(Id, text));
        public async Task Pick(string productName)
        {
            await Until(() => Renderer.Text(Id).Contains(productName));
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.ClickContaining(Id, productName));
        }
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
        public TestNavigation() => Initialize("http://localhost/", $"http://localhost/locations/{ShopId}/orders/new");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
    }

    private sealed class ComponentRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<Exception> Errors { get; } = [];
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public int Attach() => AssignRootComponentId(InstantiateComponent(typeof(NewOrder)));
        public Task Open(int id, Guid location) => RenderRootComponentAsync(id,
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NewOrder.LocationId)] = location }));

        private RenderTreeFrame[] Frames(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).ToArray();
        }

        private static string TextOf(IEnumerable<RenderTreeFrame> frames) => System.Net.WebUtility.HtmlDecode(string.Concat(frames
            .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
            .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent)));

        public string Text(int id) => TextOf(Frames(id));

        public IReadOnlyList<string> AttributeValues(int id, string name) => Frames(id)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == name)
            .Select(frame => frame.AttributeValue?.ToString() ?? "").ToArray();

        public IReadOnlyList<string> Buttons(int id) => ButtonEvents(id).Select(button => button.Text).ToArray();
        public Task Click(int id, string text) => DispatchEventAsync(ButtonEvents(id).Single(button => button.Text == text).Event, null, EventArgs.Empty);
        public Task ClickContaining(int id, string text) =>
            DispatchEventAsync(ButtonEvents(id).First(button => button.Text.Contains(text)).Event, null, EventArgs.Empty);

        public IReadOnlyList<string> AttributeNamesOnButton(int id, string text)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element || frames[index].ElementName != "button") continue;
                RenderTreeFrame[] button = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1).ToArray();
                if (TextOf(button) == text)
                    return button.Where(frame => frame.FrameType == RenderTreeFrameType.Attribute).Select(frame => frame.AttributeName).ToArray();
            }
            throw new InvalidOperationException($"Button {text} was not found.");
        }

        public string InputValue(int id, string inputId) => Input(id, inputId).Single(frame => frame.AttributeName == "value").AttributeValue?.ToString() ?? "";

        public Task Type(int id, string inputId, string text) => DispatchEventAsync(
            Input(id, inputId).Single(frame => frame.AttributeName == "oninput").AttributeEventHandlerId, null, new ChangeEventArgs { Value = text });

        private RenderTreeFrame[] Input(int id, string inputId)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element || frames[index].ElementName != "input") continue;
                RenderTreeFrame[] attributes = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1)
                    .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (attributes.Any(frame => frame.AttributeName == "id" && frame.AttributeValue?.ToString() == inputId)) return attributes;
            }
            throw new InvalidOperationException($"Input {inputId} was not found.");
        }

        private IEnumerable<(string Text, ulong Event)> ButtonEvents(int id)
        {
            RenderTreeFrame[] frames = Frames(id);
            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element || frames[index].ElementName != "button") continue;
                RenderTreeFrame[] button = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1).ToArray();
                yield return (TextOf(button), button.Single(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick").AttributeEventHandlerId);
            }
        }
    }
}
