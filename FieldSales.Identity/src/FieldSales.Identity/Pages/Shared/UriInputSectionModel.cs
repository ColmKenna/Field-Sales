namespace FieldSales.Identity.Pages.Shared;

public class UriInputSectionModel
{
    public string Label { get; }
    public string InputName { get; }
    public string ContainerId { get; }
    public string Placeholder { get; }
    public string AriaLabel { get; }
    public string ButtonText { get; }
    public ICollection<string> Values { get; }

    public UriInputSectionModel(
        string label,
        string inputName,
        string containerId,
        string placeholder,
        string ariaLabel,
        string buttonText,
        ICollection<string> values)
    {
        Label = label;
        InputName = inputName;
        ContainerId = containerId;
        Placeholder = placeholder;
        AriaLabel = ariaLabel;
        ButtonText = buttonText;
        Values = values;
    }
}
