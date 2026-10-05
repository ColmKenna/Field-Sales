namespace FieldSales.Api.Catalogue;

public sealed record ProductPricePeriod(ProductBasePrice Price, DateOnly? EffectiveThrough);
