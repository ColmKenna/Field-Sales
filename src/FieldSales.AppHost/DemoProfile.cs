namespace FieldSales.DemoData;

// Linked into the demo command so database names and the exposed SQL port stay in sync.
public static class DemoProfile
{
    public const int SqlPort = 14339;
    public const string VolumeName = "fieldsales-demo-sqlserver-data";
    public static readonly IReadOnlyList<string> Databases =
        ["IdentityDb", "IdentityConfigDb", "IdentityOperationalDb", "StaffWebDb", "CatalogueDb", "DirectoryDb"];

    public static string DatabaseName(string resourceName)
    {
        if (!Databases.Contains(resourceName, StringComparer.Ordinal))
            throw new ArgumentException("Unknown demo database.", nameof(resourceName));
        return "FieldSalesDemo_" + resourceName;
    }
}
