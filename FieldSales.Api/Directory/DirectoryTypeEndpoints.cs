using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class DirectoryTypeEndpoints
{
    public const string ServicesKey = "directory-types";
    public static void MapDirectoryTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/directory/reference-data").RequireAuthorization("HeadOfficeDirectory");
        group.MapGet("/{listKey}", (string listKey, bool? showArchived,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry,
            [FromKeyedServices(ServicesKey)] IReferenceUsageReader usage, CancellationToken ct) => GuardAsync(async () =>
        {
            var store = registry.Find(listKey); if (store is null) return Results.NotFound();
            var all = await store.ListAsync(ct);
            var visible = all.Where(type => showArchived == true || !type.IsArchived).ToArray();
            var counts = await usage.ReadManyAsync(listKey, visible.Select(type => type.Id).ToArray(), ct);
            return Results.Ok(new ReferenceListViewModel(store.Definition, registry.Definitions,
                visible.Select(type => ToItem(type, counts[type.Id])).ToArray(), all.Count(type => type.IsArchived), showArchived == true));
        }));
        group.MapGet("/{listKey}/choices", async (string listKey,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry, CancellationToken ct) =>
            registry.Find(listKey) is { } store ? Results.Ok((await store.ListAsync(ct)).Where(type => !type.IsArchived)
                .Select(type => new DirectoryTypeChoice(type.Id, type.Name, type.Description)).ToArray()) : Results.NotFound());
        group.MapGet("/{listKey}/{id:guid}/reference", async (string listKey, Guid id,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry, CancellationToken ct) =>
            registry.Find(listKey) is IDirectoryTypeListStore store && await store.ReferenceAsync(id, ct) is { } type
                ? Results.Ok(type) : Results.NotFound());
        group.MapGet("/{listKey}/{id:guid}", (string listKey, Guid id,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry,
            [FromKeyedServices(ServicesKey)] IReferenceUsageReader usage, CancellationToken ct) => GuardAsync(async () =>
            registry.Find(listKey) is { } store && await store.FindAsync(id, ct) is { } type
                ? Results.Ok(ToItem(type, await usage.ReadAsync(new(listKey, id), ct))) : Results.NotFound()));
        group.MapPost("/{listKey}", (string listKey, SaveDirectoryTypeRequest request,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry, CancellationToken ct) => SaveAsync(listKey, null, request, registry, ct));
        group.MapPut("/{listKey}/{id:guid}", (string listKey, Guid id, SaveDirectoryTypeRequest request,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry, CancellationToken ct) => SaveAsync(listKey, id, request, registry, ct));
        group.MapPost("/{listKey}/{id:guid}/retire", (string listKey, Guid id, RetireDirectoryTypeRequest request,
            [FromKeyedServices(ServicesKey)] ReferenceCatalogueRegistry registry,
            [FromKeyedServices(ServicesKey)] IReferenceUsageReader usage, CancellationToken ct) => GuardAsync(async () =>
        {
            if (registry.Find(listKey) is not IDirectoryTypeListStore store) return Results.NotFound();
            if (!Enum.IsDefined(request.Action)) return Results.BadRequest(new CustomerDirectoryError("Choose a valid action."));
            return await store.RetireVersionedAsync(id, request, usage, ct) switch
            {
                ReferenceMutationStatus.Saved => Results.Ok(new DirectoryTypeMutationResult(true)),
                ReferenceMutationStatus.Missing => Results.NotFound(),
                _ => Conflict()
            };
        }));
    }
    private static Task<IResult> SaveAsync(string key, Guid? id, SaveDirectoryTypeRequest request, ReferenceCatalogueRegistry registry, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            if (registry.Find(key) is not IDirectoryTypeListStore store) return Results.NotFound();
            var type = await store.SaveDetailsAsync(id, request, ct);
            return type is null ? Results.NotFound() : id is null ? Results.Created($"/directory/reference-data/{key}/{type.Id}", type) : Results.Ok(type);
        });
    private static ReferenceListItem ToItem(ReferenceStoredItem type, ReferenceUsage usage) => new(type.Id, type.Name, type.IsArchived, usage, type.Description, type.Version);
    private static IResult Conflict() => Results.Conflict(new CustomerDirectoryError("This type changed or is now in use. Reload it before continuing."));
    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CustomerDirectoryValidationException exception) { return Results.BadRequest(new CustomerDirectoryError(exception.Message, exception.Field)); }
        catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        { return Results.Conflict(new CustomerDirectoryError("A type with this name already exists.", "Name")); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 547 or 1205 }) { return Conflict(); }
    }
}
