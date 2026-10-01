using FieldSales.ReferenceData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed record ReferenceNameRequest(string? Name);
public sealed record ReferenceRetireRequest(ReferenceAction Action);
public sealed record ReferenceListError(string Field, string Error);

public static class ReferenceListEndpoints
{
    public static void MapReferenceListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/catalogue/reference-data").RequireAuthorization("HeadOfficeCatalogue");
        group.MapGet("/{listKey}", async (string listKey, bool? showArchived,
            ReferenceCatalogueRegistry registry, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = registry.Find(listKey);
            if (store is null) return Results.NotFound();
            try
            {
                var all = await store.ListAsync(cancellationToken);
                var visible = all.Where(item => showArchived == true || !item.IsArchived).ToArray();
                var counts = await usage.ReadManyAsync(listKey, visible.Select(item => item.Id).ToArray(), cancellationToken);
                var items = visible.Select(item => new ReferenceListItem(item.Id, item.Name, item.IsArchived, counts[item.Id])).ToArray();
                return Results.Ok(new ReferenceListViewModel(store.Definition,
                    registry.Definitions, items,
                    all.Count(item => item.IsArchived), showArchived == true));
            }
            catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        });
        group.MapGet("/{listKey}/choices", async (string listKey, ReferenceCatalogueRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var store = registry.Find(listKey);
            return store is null ? Results.NotFound()
                : Results.Ok((await store.ListAsync(cancellationToken)).Where(item => !item.IsArchived).ToArray());
        });
        group.MapGet("/{listKey}/{id:guid}", async (string listKey, Guid id,
            ReferenceCatalogueRegistry registry, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = registry.Find(listKey);
            if (store is null) return Results.NotFound();
            var item = await store.FindAsync(id, cancellationToken);
            if (item is null) return Results.NotFound();
            try { return Results.Ok(new ReferenceListItem(item.Id, item.Name, item.IsArchived,
                await usage.ReadAsync(new(listKey, id), cancellationToken))); }
            catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        });
        group.MapPost("/{listKey}", async (string listKey, ReferenceNameRequest request,
            ReferenceCatalogueRegistry registry, CancellationToken cancellationToken) =>
            await SaveAsync(listKey, null, request, registry, cancellationToken));
        group.MapPut("/{listKey}/{id:guid}/name", async (string listKey, Guid id, ReferenceNameRequest request,
            ReferenceCatalogueRegistry registry, CancellationToken cancellationToken) =>
            await SaveAsync(listKey, id, request, registry, cancellationToken));
        group.MapPost("/{listKey}/{id:guid}/retire", async (string listKey, Guid id, ReferenceRetireRequest request,
            ReferenceCatalogueRegistry registry, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = registry.Find(listKey);
            if (store is null) return Results.NotFound();
            if (!Enum.IsDefined(request.Action)) return Results.BadRequest();
            try
            {
                var status = await store.RetireAsync(id, request.Action, usage, cancellationToken);
                return status switch
                {
                    ReferenceMutationStatus.Saved => Results.NoContent(),
                    ReferenceMutationStatus.Missing => Results.NotFound(),
                    _ => Conflict()
                };
            }
            catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 or 1205 })
            { return Conflict(); }
            catch (SqlException exception) when (exception.Number == 1205) { return Conflict(); }
        });
    }

    private static async Task<IResult> SaveAsync(string listKey, Guid? id, ReferenceNameRequest request,
        ReferenceCatalogueRegistry registry, CancellationToken cancellationToken)
    {
        var store = registry.Find(listKey);
        if (store is null) return Results.NotFound();
        if (!NameRules.IsValid(request.Name, NamedReferenceItem.MaximumNameLength))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["Name"] = [NameRules.ErrorMessage(store.Definition.SingularLabel.ToLowerInvariant(), NamedReferenceItem.MaximumNameLength)] });
        try
        {
            var item = await store.SaveAsync(id, request.Name ?? string.Empty, cancellationToken);
            return item is null ? Results.NotFound() : id is null
                ? Results.Created($"/catalogue/reference-data/{listKey}/{item.Id}", item) : Results.Ok(item);
        }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        { return Results.Conflict(new ReferenceListError("Name", $"A {store.Definition.SingularLabel.ToLowerInvariant()} with this name already exists.")); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
    }

    private static IResult Conflict() => Results.Conflict(new ReferenceListError(string.Empty,
        "This item changed or is now in use. Reload it before continuing."));
}
