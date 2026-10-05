using FieldSales.Quantities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Web.Presentation;

public sealed record ProductUnitForm(string Unit, decimal? Step, decimal? Minimum)
{
    public static ProductUnitForm Normalize(ModelStateDictionary state, string unit,
        decimal? step, decimal? minimum, bool useLegacyEach = false)
    {
        if (useLegacyEach)
        {
            unit = UnitOfMeasure.Each.ToCode();
            state.Remove("Unit");
        }
        if (unit == UnitOfMeasure.Each.ToCode())
        {
            step = minimum = null;
            state.Remove("QuantityStep");
            state.Remove("MinimumQuantity");
        }
        return new(unit, step, minimum);
    }

    public static void ValidatePreview(ModelStateDictionary state, string unit)
    {
        if (!UnitOfMeasureExtensions.TryParse(unit, out _))
            state.AddModelError("Unit", "Choose Each, kg, litre or metre.");
    }
}
