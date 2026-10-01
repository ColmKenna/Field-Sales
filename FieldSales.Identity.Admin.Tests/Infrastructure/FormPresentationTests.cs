using FieldSales.Identity.Presentation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

public sealed class FormPresentationTests
{
    [Theory]
    [InlineData(null, "Name", "Name")]
    [InlineData("Input", "Name", "Input.Name")]
    [InlineData("Input", "Input.Name", "Input.Name")]
    [InlineData("Input", "", "")]
    public void ErrorMapping_PreservesSummaryAndExistingPrefix(string? prefix, string field, string expected)
    {
        ModelStateDictionary state = new();
        state.AddErrors(new Dictionary<string, string[]> { [field] = ["first", "second"] }, prefix);
        Assert.Equal(new[] { "first", "second" }, state[expected]!.Errors.Select(error => error.ErrorMessage));
    }

    [Theory]
    [InlineData(" DELETE ", true)]
    [InlineData("delete", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Confirmation_IsCaseSensitiveWithTrimmedInput(string? input, bool expected) =>
        Assert.Equal(expected, FormPresentation.MatchesConfirmation(input, "DELETE"));

    [Fact]
    public void ValidationTab_UsesFieldPrefixIncludingCollectionIndices()
    {
        Assert.Equal(1, FormPresentation.ValidationTab(["Input.RedirectUris[2]"], "Input.RedirectUris"));
        Assert.Equal(0, FormPresentation.ValidationTab(["Input.RequirePkce"], "Input.RedirectUris"));
    }
}
