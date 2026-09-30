namespace FieldSales.Api.Catalogue;

public sealed class ProductBasePrice
{
    public const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    private ProductBasePrice() { }

    public Guid ProductId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Amount { get; private set; }

    public static ProductBasePrice Create(Guid productId, decimal amount, DateOnly effectiveFrom)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("A product identity is required.", nameof(productId));
        if (amount < 0 || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Enter a non-negative base price with up to two decimal places within the supported amount.");

        return new ProductBasePrice
        {
            ProductId = productId, Amount = amount, EffectiveFrom = effectiveFrom
        };
    }
}
