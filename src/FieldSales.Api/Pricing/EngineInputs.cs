using FieldSales.Api.Catalogue;
using FieldSales.Pricing;

namespace FieldSales.Api.Pricing;

/// <summary>Stored catalogue records as the price engine (WI-058) reads them. Quantity breaks reach products with T-6.3.1.</summary>
public static class EngineInputs
{
    /// <summary>The product with its whole base price history; the engine picks the price in effect on the day.</summary>
    /// <remarks>The caller must load BasePrices.</remarks>
    public static PricedProduct ToPricedProduct(this Product product) =>
        new(product.Id, product.Unit, [.. product.BasePrices.Select(price => new DatedPrice(price.EffectiveFrom, price.Amount))]);
}
