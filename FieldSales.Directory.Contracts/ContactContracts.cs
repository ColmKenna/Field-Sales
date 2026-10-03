namespace FieldSales.Directory.Contracts;

public enum ContactStatus { Active, Inactive }

public sealed record ContactChoice(Guid Id, string Name, DirectoryTypeChoice Type, ContactStatus Status,
    string? Phone, string? Email);
public sealed record ContactLocationSummary(Guid LocationId, string Name, Guid CustomerId,
    string CustomerName, TownChoice Town, bool IsMain, string LocationVersion,
    bool ReplacementNeeded = false, bool IsRetiredMain = false);
public sealed record ContactDetails(Guid Id, string Name, DirectoryTypeChoice Type, ContactStatus Status,
    string? Phone, string? Email, string Version, IReadOnlyList<ContactLocationSummary> Locations);
public sealed record LocationContactSummary(ContactChoice Contact, bool IsMain);
public sealed record ContactLocationChoice(Guid Id, string Name, string CustomerName, string TownName)
{
    public string Label => $"{Name} — {CustomerName}, {TownName}";
}
public sealed record LocationContactsPage(Guid LocationId, string LocationName, string LocationVersion,
    ContactChoice? MainContact, IReadOnlyList<LocationContactSummary> Contacts, int InactiveCount, bool ShowInactive,
    bool ReplacementNeeded = false, ContactChoice? RetiredMainContact = null);

public sealed record ContactLocationVersion(Guid LocationId, string? Version);
public sealed record NewMainContactRequest(string? Name, Guid? ContactTypeId, string? Phone = null, string? Email = null);
public sealed record RemoveLocationContactRequest(string? ContactVersion, string? LocationVersion,
    Guid? ReplacementContactId = null, NewMainContactRequest? NewContact = null, bool NoReplacementYet = false,
    IReadOnlyList<ContactLocationVersion>? AffectedLocations = null);
public sealed record RetireMainContactRequest(string? ContactVersion, IReadOnlyList<ContactLocationVersion>? AffectedLocations);
public sealed record MainContactRetirementResult(Guid ContactId, IReadOnlyList<Guid> FlaggedLocations);

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
