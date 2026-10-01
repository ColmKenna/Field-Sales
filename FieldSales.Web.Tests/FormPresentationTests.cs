using FieldSales.Web.Presentation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Web.Tests;

public sealed class FormPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachNormalization_DiscardsStaleBinderErrorsButPreservesOtherInputs(bool legacy)
    {
        ModelStateDictionary state = new();
        state.AddModelError("QuantityStep", "not numeric");
        state.AddModelError("MinimumQuantity", "not numeric");
        state.AddModelError("Name", "required");
        state.SetModelValue("Name", "entered", "entered");
        ProductUnitForm value = ProductUnitForm.Normalize(state, legacy ? "" : "Each", 1, 2, legacy);
        Assert.Equal("Each", value.Unit);
        Assert.Null(value.Step);
        Assert.Null(value.Minimum);
        Assert.False(state.ContainsKey("QuantityStep"));
        Assert.False(state.ContainsKey("MinimumQuantity"));
        Assert.Equal("entered", state["Name"]!.AttemptedValue);
        Assert.Single(state["Name"]!.Errors);
    }

    [Fact]
    public void EditingWithoutUnit_DoesNotApplyLegacyCreationFallback()
    {
        ModelStateDictionary state = new();
        ProductUnitForm value = ProductUnitForm.Normalize(state, "", 1, 2);
        ProductUnitForm.ValidatePreview(state, value.Unit);
        Assert.Equal("", value.Unit);
        Assert.Single(state["Unit"]!.Errors);
    }

    [Fact]
    public void ErrorMapping_PreservesSummaryErrors()
    {
        ModelStateDictionary state = new();
        state.AddErrors(new Dictionary<string, string[]> { [""] = ["summary"], ["Name"] = ["field"] });
        Assert.Equal("summary", state[""]!.Errors.Single().ErrorMessage);
        Assert.Equal("field", state["Name"]!.Errors.Single().ErrorMessage);
    }
}
