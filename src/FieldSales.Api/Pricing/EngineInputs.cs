using FieldSales.Api.Catalogue;
using FieldSales.Pricing;

namespace FieldSales.Api.Pricing;

/// <summary>Stored catalogue records as the price engine (WI-058) reads them.</summary>
public static class EngineInputs
{
    /// <summary>The product with its whole base price history and its quantity breaks (WI-062 D6); the engine picks the price in
    /// effect on the day and the break the line's quantity reaches.</summary>
    /// <remarks>The caller must load BasePrices and QuantityBreaks.</remarks>
    public static PricedProduct ToPricedProduct(this Product product) =>
        new(product.Id, product.Unit, [.. product.BasePrices.Select(price => new DatedPrice(price.EffectiveFrom, price.Amount))],
            [.. product.QuantityBreaks.Select(entry => new QuantityBreak(entry.FromQuantity, entry.Price))]);
}
