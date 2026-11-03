using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

public sealed partial class NewOrderComponentTests
{
    private static async Task<Screen> OpenTea(FakeOrders orders)
    {
        Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "tea");
        await screen.Pick("Loose Herbal Tea");
        return screen;
    }

    [Fact]
    public async Task Should_StepFromMinimumAndStopThereOnDecrease_When_HalfKilogramTeaIsSelected()
    {
        await using Screen screen = await OpenTea(new());
        Assert.Equal("", screen.Renderer.InputValue(screen.Id, "line-quantity"));
        Assert.Contains("disabled", screen.Renderer.LabelledButton(screen.Id, "Decrease quantity").Keys);
        foreach (string expected in new[] { "1.0", "1.5", "2.0" })
        {
            await screen.ClickLabelled("Increase quantity");
            Assert.Equal(expected, screen.Renderer.InputValue(screen.Id, "line-quantity"));
        }
        foreach (string expected in new[] { "1.5", "1.0" })
        {
            await screen.ClickLabelled("Decrease quantity");
            Assert.Equal(expected, screen.Renderer.InputValue(screen.Id, "line-quantity"));
        }
        Assert.Contains("disabled", screen.Renderer.LabelledButton(screen.Id, "Decrease quantity").Keys);
        Assert.Contains("Quantity (kg)", screen.Renderer.Text(screen.Id));
    }

    [Theory]
    [InlineData("1.2", "Enter multiples of 0.5 kg")]
    [InlineData("1.25", "Enter multiples of 0.5 kg")]
    [InlineData("0", "Enter at least 1.0 kg in steps of 0.5 kg")]
    [InlineData("0.5", "Enter at least 1.0 kg in steps of 0.5 kg")]
    public async Task Should_RejectWithoutSavingAndAllowCorrection_When_MeasuredQuantityIsInvalid(string quantity, string message)
    {
        FakeOrders orders = new();
        await using Screen screen = await OpenTea(orders);
        await screen.Type("line-quantity", quantity);
        Assert.Contains(message, screen.Renderer.Text(screen.Id));
        Assert.Contains("disabled", screen.Renderer.AttributeNamesOnButton(screen.Id, "Add to order"));
        // Even a dispatched event cannot bypass the disabled action's validation.
        await screen.Click("Add to order");
        Assert.Empty(orders.Calls);
        Assert.Null(orders.Order);

        await screen.Type("line-quantity", "1.5");
        Assert.DoesNotContain(message, screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("disabled", screen.Renderer.AttributeNamesOnButton(screen.Id, "Add to order"));
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €7.20"));
        Assert.Equal(1.5m, Assert.Single(orders.Calls).Quantity);
    }

    [Fact]
    public async Task Should_ShowPricePerUnitAcrossPadSearchEntryAndSavedLine_When_TeaIsOrdered()
    {
        Guid categoryId = Guid.NewGuid();
        ProductSearchResult tea = Catalogue[1] with { CategoryId = categoryId };
        FakeOrders orders = new() { Products = [tea] };
        ProductSearchIndex index = new("measured", [tea], [new PadCategory(categoryId, null, "Tea")]);
        await using Screen screen = Open(orders, index: index);
        await screen.Show();
        await screen.Pick("Tea");
        Assert.Contains("€4.80 per kg", screen.Renderer.Text(screen.Id));
        await screen.ClickLabelled("Add Loose Herbal Tea");
        Assert.Equal(2, screen.Renderer.Text(screen.Id).Split("€4.80 per kg").Length - 1);
        await screen.Type("product-search", "tea");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 matching product"));
        Assert.Equal(2, screen.Renderer.Text(screen.Id).Split("€4.80 per kg").Length - 1);
        await screen.Type("line-quantity", "1.0");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €4.80"));
        Assert.Contains("1.0 kg", screen.Renderer.Text(screen.Id));
        Assert.Contains("€4.80 per kg", screen.Renderer.Text(screen.Id));
    }

    [Theory]
    [InlineData("kg", "0.5", "1.0", "1.5")]
    [InlineData("litre", "0.1", "0.1", "0.2")]
    [InlineData("metre", "0.25", "0.5", "0.75")]
    public async Task Should_UseExactSteps_When_OrderingDifferentMeasures(string unit, string step, string minimum, string expected)
    {
        decimal Number(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        ProductSearchResult product = Catalogue[1] with { Unit = unit, QuantityStep = Number(step), MinimumQuantity = Number(minimum) };
        FakeOrders orders = new() { Products = [product] };
        await using Screen screen = Open(orders, index: new ProductSearchIndex("measures", [product]));
        await screen.Show();
        await screen.Type("product-search", "tea");
        await screen.Pick("Loose Herbal Tea");
        await screen.ClickLabelled("Increase quantity");
        Assert.Equal(minimum, screen.Renderer.InputValue(screen.Id, "line-quantity"));
        await screen.ClickLabelled("Increase quantity");
        Assert.Equal(expected, screen.Renderer.InputValue(screen.Id, "line-quantity"));
        await screen.Click("Add to order");
        await screen.Until(() => orders.Order is not null && screen.Renderer.Text(screen.Id).Contains("1 line"));
        Assert.Equal((Number(expected), unit), (Assert.Single(orders.Order!.Lines).Quantity, orders.Order.Lines[0].Unit));
    }

    [Fact]
    public async Task Should_StopIncreasingAndRejectExcessiveAmounts_When_TheSupportedMaximumIsReached()
    {
        FakeOrders orders = new();
        await using Screen screen = await OpenTea(orders);
        await screen.Type("line-quantity", "999999999999.5");
        Assert.Contains("disabled", screen.Renderer.LabelledButton(screen.Id, "Increase quantity").Keys);
        Assert.DoesNotContain("disabled", screen.Renderer.AttributeNamesOnButton(screen.Id, "Add to order"));
        await screen.Type("line-quantity", "1000000000000");
        Assert.Contains("within the supported amount", screen.Renderer.Text(screen.Id));
        Assert.Contains("disabled", screen.Renderer.AttributeNamesOnButton(screen.Id, "Add to order"));
        await screen.Click("Add to order");
        Assert.Null(orders.Order);
    }
}
