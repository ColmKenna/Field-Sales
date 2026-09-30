using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldSales.Quantities;

public sealed record QuantityValidationResult(Quantity? Quantity, string? Error)
{
    public bool IsValid => Quantity.HasValue;
}

/// <summary>Product order-entry constraints, separate from the recorded quantity itself.</summary>
public sealed record QuantityRules
{
    private QuantityRules(UnitOfMeasure unit, Quantity step, Quantity minimum)
    {
        Unit = unit;
        Step = step;
        Minimum = minimum;
    }

    public UnitOfMeasure Unit { get; }
    public Quantity Step { get; }
    public Quantity Minimum { get; }

    public static bool TryCreate(string? unitCode, decimal? step, decimal? minimum,
        [NotNullWhen(true)] out QuantityRules? rules, out IReadOnlyDictionary<string, string[]> errors)
    {
        rules = null;
        Dictionary<string, string[]> validation = [];
        errors = new ReadOnlyDictionary<string, string[]>(validation);
        if (!UnitOfMeasureExtensions.TryParse(unitCode, out UnitOfMeasure unit))
        {
            validation["Unit"] = ["Choose Each, kg, litre or metre."];
            return false;
        }
        if (unit == UnitOfMeasure.Each)
        {
            // Hidden measure fields cannot change Each's whole-number order rule.
            rules = new(unit, Quantity.Create(unit, 1m), Quantity.Create(unit, 1m));
            return true;
        }

        Quantity validStep = default;
        if (step is null) validation["QuantityStep"] = ["Enter a quantity step."];
        else if (step <= 0) validation["QuantityStep"] = ["Enter a quantity step greater than zero."];
        else if (!Quantity.TryCreate(unit, step.Value, out validStep))
            validation["QuantityStep"] = ["Enter a quantity step with up to six decimal places within the supported amount."];

        decimal? effectiveMinimum = minimum ?? step;
        Quantity validMinimum = default;
        if (effectiveMinimum is null) validation["MinimumQuantity"] = ["Enter a minimum quantity."];
        else if (effectiveMinimum <= 0) validation["MinimumQuantity"] = ["Enter a minimum quantity greater than zero."];
        else if (!Quantity.TryCreate(unit, effectiveMinimum.Value, out validMinimum))
            validation["MinimumQuantity"] = ["Enter a minimum quantity with up to six decimal places within the supported amount."];

        if (validation.Count != 0) return false;
        if (validMinimum.Amount % validStep.Amount != 0)
        {
            validation["MinimumQuantity"] = ["Minimum must be a multiple of the step"];
            return false;
        }
        rules = new(unit, validStep, validMinimum);
        return true;
    }

    public QuantityValidationResult ValidateOrder(decimal amount)
    {
        if (amount < Minimum.Amount || (Unit == UnitOfMeasure.Each && amount % 1m != 0))
            return new(null, OrderValidationMessage);
        if (!Quantity.TryCreate(Unit, amount, out Quantity quantity))
        {
            return new(null, "Enter a quantity with up to six decimal places within the supported amount.");
        }
        if (amount % Step.Amount != 0)
            return new(null, OrderValidationMessage);
        return new(quantity, null);
    }

    public string OrderValidationMessage => Unit == UnitOfMeasure.Each
        ? "Enter a quantity of 1 or more"
        : $"Enter at least {Format(Minimum.Amount)} {Unit.ToCode()} in steps of {Format(Step.Amount)} {Unit.ToCode()}";

    private static string Format(decimal value) => value.ToString("0.0#####", CultureInfo.InvariantCulture);
}
