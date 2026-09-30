using System.Globalization;

namespace FieldSales.Web.Catalogue;

public sealed record ProductUnitFields(string Unit, decimal? QuantityStep, decimal? MinimumQuantity,
    decimal? PriceAmount, string? OriginalUnit = null)
{
    public string FormattedPrice => PriceAmount?.ToString("C2", CultureInfo.GetCultureInfo("en-IE")) ?? string.Empty;
    public bool PriceBasisChanged => OriginalUnit is not null && OriginalUnit != Unit;
}
