using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FieldSales.Identity.Pages.Shared;

public static class AdminComponents
{
    /// <summary>Pages declare their requirement as <c>ViewData["AdminComponents"]</c>.</summary>
    public const string ViewDataKey = "AdminComponents";

    public static AdminComponent Required(ViewDataDictionary viewData) =>
        viewData[ViewDataKey] as AdminComponent? ?? AdminComponent.None;
}
