using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class ContactStore(DirectoryDbContext db, CustomerStore customers)
{
    public Task<ContactChoice[]> ChoicesAsync(CancellationToken ct) => Choices(activeOnly: true).ToArrayAsync(ct);
    public Task<DirectoryTypeChoice[]> TypeChoicesAsync(CancellationToken ct) => db.ContactTypes.AsNoTracking()
        .Where(type => !type.IsArchived).OrderBy(type => type.Name).Select(type => new DirectoryTypeChoice(type.Id, type.Name, type.Description)).ToArrayAsync(ct);
    public Task<ContactLocationChoice[]> LocationChoicesAsync(CancellationToken ct) =>
        (from location in db.Locations.AsNoTracking()
         join customer in db.Customers on location.CustomerId equals customer.Id
         join town in db.Towns on location.TownId equals town.Id
         orderby customer.Name, location.Name, location.Id
         select new ContactLocationChoice(location.Id, location.Name, customer.Name, town.Name)).ToArrayAsync(ct);

    public async Task<ContactDetails?> FindAsync(Guid id, CancellationToken ct)
    {
        var choice = await Choices(id).SingleOrDefaultAsync(ct); if (choice is null) return null;
        byte[] version = await db.Contacts.AsNoTracking().Where(contact => contact.Id == id).Select(contact => contact.Version).SingleAsync(ct);
        var links = await db.LocationContacts.AsNoTracking().Where(link => link.ContactId == id)
            .OrderBy(link => link.Location.Name).ThenBy(link => link.LocationId)
            .Select(link => new { link.LocationId, link.Location.MainContactId }).ToArrayAsync(ct);
        List<ContactLocationSummary> locations = [];
        foreach (var link in links)
        {
            var location = (await customers.FindLocationAsync(link.LocationId, ct))!;
            locations.Add(new(location.Id, location.Name, location.CustomerId, location.CustomerName,
                location.Town, link.MainContactId == id, location.Version));
        }
        return new(choice.Id, choice.Name, choice.Type, choice.Status, choice.Phone, choice.Email, Convert.ToBase64String(version), locations);
    }
    public async Task<LocationContactsPage?> AtLocationAsync(Guid id, bool showInactive, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().SingleOrDefaultAsync(location => location.Id == id, ct);
        if (location is null) return null;
        var all = await Choices(locationId: id).ToArrayAsync(ct);
        return new(id, location.Name, Convert.ToBase64String(location.Version), all.SingleOrDefault(contact => contact.Id == location.MainContactId),
            all.Where(contact => showInactive || contact.Status == ContactStatus.Active)
                .Select(contact => new LocationContactSummary(contact, contact.Id == location.MainContactId)).ToArray(),
            all.Count(contact => contact.Status == ContactStatus.Inactive), showInactive);
    }

    public Task<ContactDetails> CreateAsync(CreateContactRequest request, CancellationToken ct) => GeographyTransactions.RunAsync(db, async () =>
    {
        if (request.LocationIds is not { Count: > 0 } || request.LocationIds.Contains(Guid.Empty))
            throw new CustomerDirectoryValidationException("LocationIds", "Choose at least one location.");
        var type = await TypeAsync(request.ContactTypeId, ct);
        List<Location> locations = [];
        foreach (var id in request.LocationIds.Distinct().Order())
            locations.Add(await RosterAsync(id, ct) ?? throw new CustomerDirectoryValidationException("LocationIds", "Choose existing locations."));
        Contact contact = Contact.Create(request.Name, type, request.Phone, request.Email, locations);
        db.Contacts.Add(contact);
        SuppressMainWrites(locations);
        await db.SaveChangesAsync(ct);
        await AdvanceRostersAsync(locations, ct);
        return (await FindAsync(contact.Id, ct))!;
    }, ct);

    public Task<ContactDetails?> EditAsync(Guid id, EditContactRequest request, CancellationToken ct) => GeographyTransactions.RunAsync(db, async () =>
    {
        var contact = await db.Contacts.FromSqlInterpolated($"SELECT * FROM [Contacts] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
        if (contact is null) return null;
        CheckVersion(contact.Version, request.Version);
        Guid[] ids = await db.LocationContacts.Where(link => link.ContactId == id).Select(link => link.LocationId).OrderBy(id => id).ToArrayAsync(ct);
        List<Location> locations = [];
        foreach (Guid locationId in ids) locations.Add((await RosterAsync(locationId, ct))!);
        contact.Edit(request.Name, await TypeAsync(request.ContactTypeId, ct), request.Phone, request.Email);
        contact.SetStatus(request.Status);
        db.Entry(contact).Property(item => item.Name).IsModified = true;
        SuppressMainWrites(locations);
        await db.SaveChangesAsync(ct);
        await AdvanceRostersAsync(locations, ct);
        return await FindAsync(id, ct);
    }, ct);

    public Task<ContactMutationResult?> LinkAsync(Guid locationId, LinkContactRequest request, CancellationToken ct) => GeographyTransactions.RunAsync(db, async () =>
    {
        var contact = await db.Contacts.FromSqlInterpolated($"SELECT * FROM [Contacts] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={request.ContactId}").SingleOrDefaultAsync(ct);
        var location = await RosterAsync(locationId, ct);
        if (location is null || contact is null) return null;
        if (location.Contacts.Any(link => link.ContactId == contact.Id)) return new ContactMutationResult(contact.Id, location.Id, AlreadyLinked: true);
        CheckVersion(location.Version, request.LocationVersion);
        bool automatic = location.MainContactId is null;
        location.LinkContact(contact);
        SuppressMainWrites([location]);
        await db.SaveChangesAsync(ct);
        await AdvanceRostersAsync([location], ct);
        return new ContactMutationResult(contact.Id, location.Id, AutomaticallyMadeMain: automatic && location.MainContactId == contact.Id);
    }, ct);

    public Task<SetMainContactResult?> SetMainAsync(Guid locationId, SetMainContactRequest request, CancellationToken ct) => GeographyTransactions.RunAsync(db, async () =>
    {
        var location = await RosterAsync(locationId, ct); if (location is null) return null;
        CheckVersion(location.Version, request.LocationVersion);
        var contact = location.Contacts.SingleOrDefault(link => link.ContactId == request.ContactId)?.Contact
            ?? throw new CustomerDirectoryValidationException("ContactId", "Choose an active contact linked to this location.");
        try { location.SetMain(contact, request.ExpectedMainContactId, request.ConfirmReplacement); }
        catch (MainContactReplacementRequiredException exception)
        {
            var outgoing = (await Choices(exception.OutgoingContactId).SingleAsync(ct));
            var proposed = (await Choices(contact.Id).SingleAsync(ct));
            return new SetMainContactResult(null, new(outgoing, proposed, Convert.ToBase64String(location.Version)));
        }
        db.Entry(location).Property(item => item.Name).IsModified = true;
        await db.SaveChangesAsync(ct);
        return new SetMainContactResult(new(contact.Id, location.Id));
    }, ct);

    private IQueryable<ContactChoice> Choices(Guid? id = null, Guid? locationId = null, bool activeOnly = false) =>
        from contact in db.Contacts.AsNoTracking()
        where (id == null || contact.Id == id) && (locationId == null || db.LocationContacts.Any(link => link.ContactId == contact.Id && link.LocationId == locationId))
            && (!activeOnly || contact.Status == ContactStatus.Active)
        join type in db.ContactTypes on contact.ContactTypeId equals type.Id
        orderby contact.Name, contact.Id
        select new ContactChoice(contact.Id, contact.Name, new(type.Id, type.Name, type.Description, type.IsArchived), contact.Status, contact.Phone, contact.Email);
    private async Task<ContactType> TypeAsync(Guid? id, CancellationToken ct) =>
        id is Guid value && await db.ContactTypes.SingleOrDefaultAsync(type => type.Id == value, ct) is { } type
            ? type : throw new CustomerDirectoryValidationException("ContactTypeId", "Choose an active contact type.");
    private Task<Location?> RosterAsync(Guid id, CancellationToken ct) => db.Locations
        .FromSqlInterpolated($"SELECT * FROM [Locations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
        .Include(location => location.Contacts).ThenInclude(link => link.Contact).SingleOrDefaultAsync(ct);
    private void SuppressMainWrites(IReadOnlyList<Location> locations)
    {
        db.ChangeTracker.DetectChanges();
        foreach (var location in locations) db.Entry(location).Property(item => item.MainContactId).IsModified = false;
    }
    private async Task AdvanceRostersAsync(IReadOnlyList<Location> locations, CancellationToken ct)
    {
        foreach (var location in locations)
        {
            await db.Entry(location).ReloadAsync(ct); // Triggers may have changed the pointer and rowversion.
            db.Entry(location).Property(item => item.Name).IsModified = true;
        }
        await db.SaveChangesAsync(ct);
    }
    private static void CheckVersion(byte[] current, string? supplied)
    {
        byte[] version;
        try { version = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw new CustomerDirectoryValidationException("Version", "Reload before saving this change."); }
        if (version.Length != 8) throw new CustomerDirectoryValidationException("Version", "Reload before saving this change.");
        if (!current.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
    }
}
