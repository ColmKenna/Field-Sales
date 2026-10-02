using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Directory;

public sealed class Location
{
    public const int MaximumEircodeLength = 20;
    private readonly List<LocationContact> _contacts = [];
    private Location() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid TownId { get; private set; }
    public string? Eircode { get; private set; }
    public Guid? LocationTypeId { get; private set; }
    public Guid? MainContactId { get; private set; }
    public IReadOnlyList<LocationContact> Contacts => _contacts.AsReadOnly();
    public byte[] Version { get; private set; } = [];

    internal static Location Create(Guid customerId, string? name, Guid? townId, string? eircode)
    {
        if (customerId == Guid.Empty)
            throw new CustomerDirectoryValidationException("CustomerId", "Choose a customer.");
        Location location = new() { Id = Guid.NewGuid(), CustomerId = customerId };
        location.Edit(name, townId, eircode);
        return location;
    }

    // The store must validate new Town assignments against the current hierarchy
    // and duplicate names inside the same transaction. Customer ownership is not editable here.
    internal void Edit(string? name, Guid? townId, string? eircode)
    {
        string validName = CustomerDirectoryFields.Name(name, "location");
        if (townId is null || townId == Guid.Empty)
            throw new CustomerDirectoryValidationException("TownId", "Choose a town");
        string? validEircode = string.IsNullOrWhiteSpace(eircode) ? null : eircode.Trim();
        if (validEircode is not null && (validEircode.Length > MaximumEircodeLength || validEircode.Any(char.IsControl)))
            throw new CustomerDirectoryValidationException("Eircode", "Enter an Eircode of up to 20 characters.");
        Name = validName;
        NormalizedName = validName.ToUpperInvariant();
        TownId = townId.Value;
        Eircode = validEircode;
    }
    internal void SetType(Guid? typeId) => LocationTypeId = typeId;

    // Load the roster before calling these methods; the store locks the Location
    // and validates its rowversion in the same serializable write transaction.
    internal bool LinkContact(Contact contact)
    {
        if (contact.Status != ContactStatus.Active)
            throw new CustomerDirectoryValidationException("ContactId", "Choose an active contact.");
        if (_contacts.Any(link => link.ContactId == contact.Id)) return false;
        var link = LocationContact.Create(this, contact);
        _contacts.Add(link); contact.Attach(link);
        EnsureFirstActiveMain(contact); return true;
    }
    internal void EnsureFirstActiveMain(Contact contact)
    {
        if (MainContactId is null && contact.Status == ContactStatus.Active
            && _contacts.Any(link => link.ContactId == contact.Id)) MainContactId = contact.Id;
    }
    internal void SetMain(Contact contact, Guid? expectedMainContactId, bool confirmReplacement)
    {
        if (MainContactId != expectedMainContactId) throw new MainContactChangedException();
        if (contact.Status != ContactStatus.Active || !_contacts.Any(link => link.ContactId == contact.Id))
            throw new CustomerDirectoryValidationException("ContactId", "Choose an active contact linked to this location.");
        if (MainContactId is Guid outgoing && outgoing != contact.Id && !confirmReplacement)
            throw new MainContactReplacementRequiredException(outgoing);
        MainContactId = contact.Id;
    }
}

internal static class CustomerDirectoryFields
{
    public const int MaximumNameLength = 200;
    public static string Name(string? value, string label)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaximumNameLength || trimmed.Any(char.IsControl))
            throw new CustomerDirectoryValidationException("Name", $"Enter a {label} name of up to 200 characters.");
        return trimmed;
    }
}

public sealed class CustomerDirectoryValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
