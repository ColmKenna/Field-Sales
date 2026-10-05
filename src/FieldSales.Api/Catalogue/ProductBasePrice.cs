namespace FieldSales.Api.Catalogue;

public sealed class ProductBasePrice
{
    public const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    public const string InvalidAmountMessage = "Enter a non-negative base price with up to two decimal places within the supported amount.";

    public static bool IsValidAmount(decimal amount) =>
        amount >= 0 && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;

    private ProductBasePrice() { }

    public Guid ProductId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Amount { get; private set; }

    public static ProductBasePrice Create(Guid productId, decimal amount, DateOnly effectiveFrom)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("A product identity is required.", nameof(productId));
        if (!IsValidAmount(amount))
            throw new ArgumentOutOfRangeException(nameof(amount),
                InvalidAmountMessage);

        return new ProductBasePrice
        {
            ProductId = productId, Amount = amount, EffectiveFrom = effectiveFrom
        };
    }
}
