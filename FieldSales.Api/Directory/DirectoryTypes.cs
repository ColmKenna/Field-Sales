using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Directory;

public abstract class DirectoryType : NamedReferenceItem
{
    public const int MaximumDescriptionLength = 2000;
    public string? Description { get; private set; }
    internal void SetDescription(string? value)
    {
        string? description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (description is not null && (description.Length > MaximumDescriptionLength
            || description.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'))))
            throw new CustomerDirectoryValidationException("Description", "Enter a description of up to 2000 characters.");
        Description = description;
    }
}

public sealed class LocationType : DirectoryType
{
    private LocationType() { }
    protected override string ItemLabel => "location type";
    internal static LocationType Create(string name, string? description)
    {
        LocationType type = new(); type.Initialize(name); type.SetDescription(description); return type;
    }
}

public sealed class ContactType : DirectoryType
{
    private ContactType() { }
    protected override string ItemLabel => "contact type";
    internal static ContactType Create(string name, string? description)
    {
        ContactType type = new(); type.Initialize(name); type.SetDescription(description); return type;
    }
}
