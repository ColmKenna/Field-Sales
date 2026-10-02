namespace FieldSales.ReferenceData;

public static class ReferenceListKeys
{
    public const string Brands = "brands";
    public const string Profiles = "profiles";
    public const string AttributeNames = "attribute-names";
    public const string Suppliers = "suppliers";
    public const string RestrictionGroups = "restriction-groups";
    public const string LocationTypes = "location-types";
    public const string ContactTypes = "contact-types";
    public static bool IsDirectoryType(string key) => key is LocationTypes or ContactTypes;
    public static IReadOnlyList<ReferenceListDefinition> DirectoryTypes { get; } =
    [
        new(LocationTypes, "Location Type", "Location Types", ["locations"]),
        new(ContactTypes, "Contact Type", "Contact Types", ["contacts"])
    ];
    public static IReadOnlyList<ReferenceListDefinition> SwitcherLists { get; } =
    [
        new(Brands, "Brand", "Brands", ["products"]),
        new(Profiles, "Product Profile", "Product Profiles", ["products"]),
        new(AttributeNames, "Attribute", "Attribute names", ["products"]),
        new(Suppliers, "Supplier", "Suppliers", ["products"]),
        new(RestrictionGroups, "Restriction Group", "Restriction Groups", ["products"]),
        .. DirectoryTypes
    ];
}
