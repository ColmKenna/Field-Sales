using System.Globalization;

namespace FieldSales.Web.Catalogue;

public sealed record ProductUnitFields(string Unit, decimal? QuantityStep, decimal? MinimumQuantity,
    decimal? PriceAmount, string? OriginalUnit = null)
{
    public string FormattedPrice => PriceAmount is not decimal amount ? string.Empty
        : amount % 0.01m == 0 ? amount.ToString("C2", CultureInfo.GetCultureInfo("en-IE"))
        : "€" + amount.ToString("G29", CultureInfo.InvariantCulture);
    public bool PriceBasisChanged => OriginalUnit is not null && OriginalUnit != Unit;
}
