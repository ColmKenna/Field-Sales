namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-045 checkpoint: Each entry before adding measure-based controls.</summary>
public sealed partial class NewOrderComponentTests
{
    [Fact]
    public async Task Should_OpenWithAnEmptyQuantity_When_AnEachProductIsSelected()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        Assert.Equal("", screen.Renderer.InputValue(screen.Id, "line-quantity"));
        Assert.Empty(orders.Calls);
        Assert.Null(orders.Order);
    }

    [Fact]
    public async Task Should_SaveOneWholeUnit_When_AnEachQuantityIsOne()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        await screen.Type("line-quantity", "1");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €12.50"));

        Assert.Equal(1m, Assert.Single(orders.Calls).Quantity);
        var line = Assert.Single(orders.Order!.Lines);
        Assert.Equal((1m, "Each"), (line.Quantity, line.Unit));
        Assert.DoesNotContain("per Each", screen.Renderer.Text(screen.Id));
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("0", "0")]
    [InlineData("-3", "0")]
    [InlineData("1.5", "1.5")]
    [InlineData("twelve", "0")]
    public async Task Should_KeepTheEnteredQuantityAndShowTheStoreError_When_AnEachQuantityIsRefused(
        string entered, string submitted)
    {
        FakeOrders orders = new() { Refuse = "Enter a quantity of 1 or more" };
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        await screen.Type("line-quantity", entered);
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Enter a quantity of 1 or more"));

        Assert.Equal(decimal.Parse(submitted, System.Globalization.CultureInfo.InvariantCulture), Assert.Single(orders.Calls).Quantity);
        Assert.Equal(entered, screen.Renderer.InputValue(screen.Id, "line-quantity"));
        Assert.Contains("alert", screen.Renderer.AttributeValues(screen.Id, "role"));
        Assert.Contains("No products yet.", screen.Renderer.Text(screen.Id));
        Assert.Null(orders.Order);
    }
}
