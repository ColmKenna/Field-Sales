namespace FieldSales.Directory.Contracts;

public sealed record CustomerSummary(Guid Id, string Name, int LocationCount);
public sealed record CustomerDetails(Guid Id, string Name, string Version, IReadOnlyList<LocationSummary> Locations);
public sealed record LocationSummary(Guid Id, string Name, TownChoice Town, string? Eircode, string Version,
    DirectoryTypeChoice? Type = null, LocationPosition? Position = null);
public sealed record LocationDetails(Guid Id, Guid CustomerId, string CustomerName, string Name,
    TownChoice Town, string? Eircode, string Version, DirectoryTypeChoice? Type = null, LocationPosition? Position = null);

// FirstLocation is nullable on the wire so an omitted object can produce a useful
// field error; the creation service must reject its absence before any writes.
public sealed record CreateCustomerRequest(string? Name, CreateLocationRequest? FirstLocation);
public sealed record CreateLocationRequest(string? Name, Guid? TownId, string? Eircode = null,
    bool ConfirmDuplicateName = false, Guid? LocationTypeId = null);
public sealed record EditLocationRequest(string? Name, Guid? TownId, string? Eircode, string? Version,
    bool ConfirmDuplicateName = false, Guid? LocationTypeId = null);
public sealed record CustomerDirectoryError(string Error, string? Field = null, bool RequiresDuplicateConfirmation = false);
