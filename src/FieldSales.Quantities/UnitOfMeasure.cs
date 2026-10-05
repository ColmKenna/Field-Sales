namespace FieldSales.Quantities;

public enum UnitOfMeasure { Each, Kilogram, Litre, Metre }

public static class UnitOfMeasureExtensions
{
    public static string ToCode(this UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Each => "Each",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Litre => "litre",
        UnitOfMeasure.Metre => "metre",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), "Choose a supported unit.")
    };

    public static bool TryParse(string? code, out UnitOfMeasure unit)
    {
        UnitOfMeasure? parsed = code switch
        {
            "Each" => UnitOfMeasure.Each,
            "kg" => UnitOfMeasure.Kilogram,
            "litre" => UnitOfMeasure.Litre,
            "metre" => UnitOfMeasure.Metre,
            _ => null
        };
        unit = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }
}
