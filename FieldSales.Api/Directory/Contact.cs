using System.ComponentModel.DataAnnotations;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Directory;

public sealed class Contact
{
    public const int MaximumPhoneLength = 50;
    public const int MaximumEmailLength = 254;
    private readonly List<LocationContact> _locations = [];
    private Contact() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid ContactTypeId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public ContactStatus Status { get; private set; } = ContactStatus.Active;
    public byte[] Version { get; private set; } = [];
    public IReadOnlyList<LocationContact> Locations => _locations.AsReadOnly();

    // The store loads/validates every requested Location before this factory and
    // saves Contact, links and first-Main choices in one DirectoryDb transaction.
    internal static Contact Create(string? name, ContactType type, string? phone, string? email,
        IReadOnlyList<Location> locations)
    {
        if (locations.Count == 0)
            throw new CustomerDirectoryValidationException("LocationIds", "Choose at least one location.");
        Contact contact = new() { Id = Guid.NewGuid() };
        contact.Edit(name, type, phone, email);
        foreach (var location in locations.DistinctBy(location => location.Id)) location.LinkContact(contact);
        return contact;
    }

    internal void Edit(string? name, ContactType type, string? phone, string? email)
    {
        string validName = CustomerDirectoryFields.Name(name, "contact");
        if (type.IsArchived && type.Id != ContactTypeId)
            throw new CustomerDirectoryValidationException("ContactTypeId", "Choose an active contact type.");
        string? validPhone = Detail(phone, MaximumPhoneLength, "Phone", new PhoneAttribute());
        string? validEmail = Detail(email, MaximumEmailLength, "Email", new EmailAddressAttribute());
        Name = validName; ContactTypeId = type.Id; Phone = validPhone; Email = validEmail;
    }

    // Load all linked Locations, not just the shop whose screen initiated the edit.
    // The planned database guard independently protects against partial loading/bypass.
    internal void SetStatus(ContactStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new CustomerDirectoryValidationException("Status", "Choose Active or Inactive.");
        if (status == ContactStatus.Inactive && _locations.Any(link => link.IsMain))
            throw new CustomerDirectoryValidationException("Status",
                "This contact is Main at a location. Choose a replacement before marking them inactive.");
        Status = status;
        // Normally every linked shop already has a Main. This also repairs an
        // inactive-only legacy roster when its first Active person becomes available.
        if (status == ContactStatus.Active)
            foreach (var link in _locations) link.Location.EnsureFirstActiveMain(this);
    }

    internal void Attach(LocationContact link) => _locations.Add(link);
    private static string? Detail(string? value, int maximum, string field, ValidationAttribute format)
    {
        string? trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is not null && (trimmed.Length > maximum || trimmed.Any(char.IsControl) || !format.IsValid(trimmed)))
            throw new CustomerDirectoryValidationException(field, $"Enter a valid {field.ToLowerInvariant()} of up to {maximum} characters.");
        return trimmed;
    }
}
