using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public interface IDirectoryTypeListStore : IReferenceListStore
{
    Task<ReferenceStoredItem?> SaveDetailsAsync(Guid? id, SaveDirectoryTypeRequest request, CancellationToken ct);
    Task<ReferenceMutationStatus> RetireVersionedAsync(Guid id, RetireDirectoryTypeRequest request,
        IReferenceUsageReader usage, CancellationToken ct);
    Task<DirectoryTypeChoice?> ReferenceAsync(Guid id, CancellationToken ct);
}

public class DirectoryTypeListStore<T>(DirectoryDbContext db, ReferenceListDefinition definition,
    Func<string, string?, T> create) : IDirectoryTypeListStore where T : DirectoryType
{
    public ReferenceListDefinition Definition => definition;
    public async Task<IReadOnlyList<ReferenceStoredItem>> ListAsync(CancellationToken ct) =>
        await db.Set<T>().AsNoTracking().OrderBy(type => type.Name).ThenBy(type => type.Id)
            .Select(type => new ReferenceStoredItem(type.Id, type.Name, type.IsArchived, type.Description,
                Convert.ToBase64String(type.Version))).ToArrayAsync(ct);
    public Task<ReferenceStoredItem?> FindAsync(Guid id, CancellationToken ct) => db.Set<T>().AsNoTracking()
        .Where(type => type.Id == id).Select(type => new ReferenceStoredItem(type.Id, type.Name, type.IsArchived,
            type.Description, Convert.ToBase64String(type.Version))).SingleOrDefaultAsync(ct);
    public Task<DirectoryTypeChoice?> ReferenceAsync(Guid id, CancellationToken ct) => db.Set<T>().AsNoTracking()
        .Where(type => type.Id == id).Select(type => new DirectoryTypeChoice(type.Id, type.Name, type.Description, type.IsArchived))
        .SingleOrDefaultAsync(ct);

    public async Task<ReferenceStoredItem?> SaveAsync(Guid? id, string name, CancellationToken ct)
    {
        var current = id is Guid value ? await FindAsync(value, ct) : null;
        if (id is not null && current is null) return null;
        return await SaveDetailsAsync(id, new(name, current?.Description, current?.Version), ct);
    }
    public Task<ReferenceStoredItem?> SaveDetailsAsync(Guid? id, SaveDirectoryTypeRequest request, CancellationToken ct) =>
        GeographyTransactions.RunAsync(db, async () =>
        {
            T? type = id is Guid value ? await db.Set<T>().SingleOrDefaultAsync(type => type.Id == value, ct) : null;
            if (id is not null && type is null) return null;
            if (!NameRules.IsValid(request.Name, NamedReferenceItem.MaximumNameLength) || request.Name.Any(char.IsControl))
                throw new CustomerDirectoryValidationException("Name", NameRules.ErrorMessage(definition.SingularLabel.ToLowerInvariant(), 200));
            if (type is null) { type = create(request.Name, request.Description); db.Set<T>().Add(type); }
            else
            {
                CheckVersion(type, request.Version);
                type.Rename(request.Name); type.SetDescription(request.Description);
                db.Entry(type).Property(item => item.Name).IsModified = true;
            }
            await db.SaveChangesAsync(ct);
            return new ReferenceStoredItem(type.Id, type.Name, type.IsArchived, type.Description, Convert.ToBase64String(type.Version));
        }, ct);

    public async Task<ReferenceMutationStatus> RetireAsync(Guid id, ReferenceAction action, IReferenceUsageReader usage, CancellationToken ct)
    {
        var current = await FindAsync(id, ct);
        return current is null ? ReferenceMutationStatus.Missing : await RetireVersionedAsync(id, new(action, current.Version), usage, ct);
    }
    public Task<ReferenceMutationStatus> RetireVersionedAsync(Guid id, RetireDirectoryTypeRequest request,
        IReferenceUsageReader usage, CancellationToken ct) => GeographyTransactions.RunAsync(db, async () =>
        {
            var type = await db.Set<T>().SingleOrDefaultAsync(type => type.Id == id, ct);
            if (type is null) return ReferenceMutationStatus.Missing;
            CheckVersion(type, request.Version);
            var currentUsage = await usage.ReadAsync(new(definition.Key, id), ct);
            if (ReferenceRetirementPolicy.Decide(type.IsArchived, currentUsage) != request.Action) return ReferenceMutationStatus.Conflict;
            switch (request.Action)
            {
                case ReferenceAction.Delete: db.Set<T>().Remove(type); break;
                case ReferenceAction.Archive: type.Archive(); break;
                case ReferenceAction.Unarchive: type.Unarchive(); break;
                default: throw new CustomerDirectoryValidationException("Action", "Choose a valid action.");
            }
            await db.SaveChangesAsync(ct); return ReferenceMutationStatus.Saved;
        }, ct);

    private static void CheckVersion(T type, string? supplied)
    {
        byte[] version;
        try { version = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw new CustomerDirectoryValidationException("Version", "Reload this type before continuing."); }
        if (version.Length != 8) throw new CustomerDirectoryValidationException("Version", "Reload this type before continuing.");
        if (!type.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
    }
}

public sealed class LocationTypeListStore(DirectoryDbContext db) : DirectoryTypeListStore<LocationType>(db,
    ReferenceListKeys.DirectoryTypes[0], LocationType.Create);
public sealed class ContactTypeListStore(DirectoryDbContext db) : DirectoryTypeListStore<ContactType>(db,
    ReferenceListKeys.DirectoryTypes[1], ContactType.Create);
