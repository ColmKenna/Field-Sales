namespace FieldSales.Quantities;

/// <summary>An exact amount with its recorded unit. Zero is valid for a stock count.</summary>
public readonly record struct Quantity
{
    public const int Precision = 18;
    public const int Scale = 6;
    public const decimal MaximumAmount = 999_999_999_999.999999m;
    public const decimal SmallestIncrement = 0.000001m;

    private Quantity(UnitOfMeasure unit, decimal amount)
    {
        Unit = unit;
        Amount = amount;
    }

    public UnitOfMeasure Unit { get; }
    public decimal Amount { get; }

    public static bool TryCreate(UnitOfMeasure unit, decimal amount, out Quantity quantity)
    {
        quantity = default;
        if (!Enum.IsDefined(unit) || amount < 0 || amount > MaximumAmount
            || amount % SmallestIncrement != 0
            || (unit == UnitOfMeasure.Each && amount % 1m != 0))
            return false;
        quantity = new Quantity(unit, amount);
        return true;
    }

    public static Quantity Create(UnitOfMeasure unit, decimal amount)
    {
        if (!TryCreate(unit, amount, out Quantity quantity))
            throw new ArgumentException("Use a supported unit and a non-negative quantity within the supported amount: whole numbers for Each, up to six decimal places for measures.");
        return quantity;
    }
}
