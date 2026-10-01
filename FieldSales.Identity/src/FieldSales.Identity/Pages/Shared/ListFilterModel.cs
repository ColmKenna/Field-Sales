namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     The single-input filter form on a list page. Clear renders only while a filter is
///     active, matching the two multi-field filters that already behaved this way.
/// </summary>
public class ListFilterModel
{
    public string InputId { get; }
    public string LabelText { get; }
    public string Placeholder { get; }
    public string? Value { get; }
    public string InputName { get; }

    public ListFilterModel(
        string inputId,
        string labelText,
        string placeholder,
        string? value,
        string inputName = "Filter")
    {
        InputId = inputId;
        LabelText = labelText;
        Placeholder = placeholder;
        Value = value;
        InputName = inputName;
    }
}
