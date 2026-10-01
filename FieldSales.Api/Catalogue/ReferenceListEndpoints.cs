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
            IEnumerable<IReferenceListStore> stores, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = stores.SingleOrDefault(store => store.Definition.Key == listKey);
            if (store is null) return Results.NotFound();
            try
            {
                var all = await store.ListAsync(cancellationToken);
                List<ReferenceListItem> items = [];
                foreach (var item in all.Where(item => showArchived == true || !item.IsArchived))
                    items.Add(new(item.Id, item.Name, item.IsArchived,
                        await usage.ReadAsync(new(listKey, item.Id), cancellationToken)));
                return Results.Ok(new ReferenceListViewModel(store.Definition,
                    stores.Select(store => store.Definition).ToArray(), items,
                    all.Count(item => item.IsArchived), showArchived == true));
            }
            catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        });
        group.MapGet("/{listKey}/choices", async (string listKey, IEnumerable<IReferenceListStore> stores,
            CancellationToken cancellationToken) =>
        {
            var store = stores.SingleOrDefault(store => store.Definition.Key == listKey);
            return store is null ? Results.NotFound()
                : Results.Ok((await store.ListAsync(cancellationToken)).Where(item => !item.IsArchived).ToArray());
        });
        group.MapGet("/{listKey}/{id:guid}", async (string listKey, Guid id,
            IEnumerable<IReferenceListStore> stores, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = stores.SingleOrDefault(store => store.Definition.Key == listKey);
            if (store is null) return Results.NotFound();
            var item = await store.FindAsync(id, cancellationToken);
            if (item is null) return Results.NotFound();
            try { return Results.Ok(new ReferenceListItem(item.Id, item.Name, item.IsArchived,
                await usage.ReadAsync(new(listKey, id), cancellationToken))); }
            catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        });
        group.MapPost("/{listKey}", async (string listKey, ReferenceNameRequest request,
            IEnumerable<IReferenceListStore> stores, CancellationToken cancellationToken) =>
            await SaveAsync(listKey, null, request, stores, cancellationToken));
        group.MapPut("/{listKey}/{id:guid}/name", async (string listKey, Guid id, ReferenceNameRequest request,
            IEnumerable<IReferenceListStore> stores, CancellationToken cancellationToken) =>
            await SaveAsync(listKey, id, request, stores, cancellationToken));
        group.MapPost("/{listKey}/{id:guid}/retire", async (string listKey, Guid id, ReferenceRetireRequest request,
            IEnumerable<IReferenceListStore> stores, IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        {
            var store = stores.SingleOrDefault(store => store.Definition.Key == listKey);
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
        IEnumerable<IReferenceListStore> stores, CancellationToken cancellationToken)
    {
        var store = stores.SingleOrDefault(store => store.Definition.Key == listKey);
        if (store is null) return Results.NotFound();
        try
        {
            var item = await store.SaveAsync(id, request.Name ?? string.Empty, cancellationToken);
            return item is null ? Results.NotFound() : id is null
                ? Results.Created($"/catalogue/reference-data/{listKey}/{item.Id}", item) : Results.Ok(item);
        }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["Name"] = [exception.Message.Split(" (Parameter", StringSplitOptions.None)[0]] }); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return Results.Conflict(new ReferenceListError("Name", $"A {store.Definition.SingularLabel.ToLowerInvariant()} with this name already exists.")); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
    }

    private static IResult Conflict() => Results.Conflict(new ReferenceListError(string.Empty,
        "This item changed or is now in use. Reload it before continuing."));
}
