namespace FieldSales.Api.Directory;

public sealed class Customer
{
    private readonly List<Location> _locations = [];
    private Customer() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public byte[] Version { get; private set; } = [];
    public IReadOnlyList<Location> Locations => _locations.AsReadOnly();

    // Creation always includes a first Location. The store will persist the complete
    // aggregate in one transaction after validating the Town and all request fields.
    internal static Customer Create(string? name, string? firstLocationName, Guid? townId, string? eircode)
    {
        Customer customer = new() { Id = Guid.NewGuid(), Name = CustomerDirectoryFields.Name(name, "customer") };
        try { customer.AddLocation(firstLocationName, townId, eircode); }
        catch (CustomerDirectoryValidationException exception)
        { throw new CustomerDirectoryValidationException("FirstLocation." + exception.Field, exception.Message); }
        return customer;
    }

    internal Location AddLocation(string? name, Guid? townId, string? eircode)
    {
        Location location = Location.Create(Id, name, townId, eircode);
        _locations.Add(location);
        return location;
    }
}
