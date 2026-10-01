namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     A grid of scope checkboxes bound to one collection property.
/// </summary>
/// <remarks>
///     Covers the plain case only. The identity-resource grid on the Client Permissions page
///     looks similar but is not the same component: "openid" there renders a disabled checkbox
///     plus a hidden input and a required marker, because it cannot be deselected. Folding that
///     branch in would make this partial answer a question it should not know about.
/// </remarks>
public class ScopeCheckboxGridModel
{
    public IEnumerable<string> Scopes { get; }
    public ICollection<string> Selected { get; }

    /// <summary>
    ///     Names the checkbox group for assistive technology. Every call site already shows a
    ///     heading or tab label, so the legend is rendered visually hidden rather than repeated.
    /// </summary>
    public string Legend { get; }

    public string InputName { get; }

    public ScopeCheckboxGridModel(
        IEnumerable<string> scopes,
        ICollection<string> selected,
        string legend,
        string inputName = "Input.AllowedScopes")
    {
        Scopes = scopes;
        Selected = selected;
        Legend = legend;
        InputName = inputName;
    }
}
