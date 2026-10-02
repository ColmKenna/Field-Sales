namespace FieldSales.Api.Directory;

public sealed class LocationContact
{
    private LocationContact() { }
    private LocationContact(Location location, Contact contact)
    { Location = location; Contact = contact; LocationId = location.Id; ContactId = contact.Id; }
    public Guid LocationId { get; private set; }
    public Guid ContactId { get; private set; }
    public Location Location { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;
    public bool IsMain => Location.MainContactId == ContactId;

    internal static LocationContact Create(Location location, Contact contact) => new(location, contact);
}

public sealed class MainContactChangedException() : Exception("This location's Main Contact changed. Reload before continuing.");
public sealed class MainContactReplacementRequiredException(Guid outgoingContactId)
    : Exception("Confirm replacement of the existing Main Contact.")
{
    public Guid OutgoingContactId { get; } = outgoingContactId;
}
