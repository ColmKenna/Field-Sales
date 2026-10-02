namespace FieldSales.Directory.Contracts;

public enum ContactStatus { Active, Inactive }

public sealed record ContactChoice(Guid Id, string Name, DirectoryTypeChoice Type, ContactStatus Status,
    string? Phone, string? Email);
public sealed record ContactLocationSummary(Guid LocationId, string Name, Guid CustomerId,
    string CustomerName, TownChoice Town, bool IsMain, string LocationVersion);
public sealed record ContactDetails(Guid Id, string Name, DirectoryTypeChoice Type, ContactStatus Status,
    string? Phone, string? Email, string Version, IReadOnlyList<ContactLocationSummary> Locations);
public sealed record LocationContactSummary(ContactChoice Contact, bool IsMain);
public sealed record ContactLocationChoice(Guid Id, string Name, string CustomerName, string TownName)
{
    public string Label => $"{Name} — {CustomerName}, {TownName}";
}
public sealed record LocationContactsPage(Guid LocationId, string LocationName, string LocationVersion,
    ContactChoice? MainContact, IReadOnlyList<LocationContactSummary> Contacts, int InactiveCount, bool ShowInactive);

public sealed record CreateContactRequest(string? Name, Guid? ContactTypeId, IReadOnlyList<Guid>? LocationIds,
    string? Phone = null, string? Email = null);
public sealed record EditContactRequest(string? Name, Guid? ContactTypeId, string? Phone, string? Email,
    ContactStatus Status, string? Version);
public sealed record LinkContactRequest(Guid ContactId, string? LocationVersion);
// The browser must return the outgoing Main it was shown, not just a boolean confirmation.
public sealed record SetMainContactRequest(Guid ContactId, string? LocationVersion,
    Guid? ExpectedMainContactId, bool ConfirmReplacement = false);
public sealed record MainContactConfirmation(ContactChoice Outgoing, ContactChoice Proposed, string LocationVersion)
{
    public string Question => $"Replace {Outgoing.Name} as main contact?";
}
public sealed record ContactMutationResult(Guid ContactId, Guid? LocationId = null,
    bool AlreadyLinked = false, bool AutomaticallyMadeMain = false);
public sealed record SetMainContactResult(ContactMutationResult? Mutation, MainContactConfirmation? Confirmation = null);
public sealed record ContactDirectoryError(string Error, string? Field = null,
    MainContactConfirmation? Confirmation = null);
