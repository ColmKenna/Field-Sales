using System.Text;
using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class GeographyEndpoints
{
    public static void MapGeographyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/directory/geography").RequireAuthorization("HeadOfficeDirectory");
        group.MapGet("/", (Guid? regionId, Guid? countyId, bool? showArchived, GeographyStore store, CancellationToken ct) =>
            GuardAsync(async () => await store.PageAsync(regionId, countyId, ct, showArchived == true) is { } page ? Results.Ok(page) : Results.NotFound()));
        group.MapGet("/town-choices", async (GeographyStore store, CancellationToken ct) => Results.Ok(await store.ChoicesAsync(ct)));
        group.MapGet("/towns/{id:guid}/reference", async (Guid id, GeographyStore store, CancellationToken ct) =>
            await store.TownReferenceAsync(id, ct) is { } town ? Results.Ok(town) : Results.NotFound());
        MapNames<Region>(group, "regions", (request, db, ct) =>
        {
            if (request.ParentId is not null) throw new GeographyValidationException("Regions cannot have a parent.");
            return Task.FromResult(new Region(request.Name!));
        });
        MapNames<County>(group, "counties", async (request, db, ct) =>
        {
            if (request.ParentId is not Guid id || !await db.Regions.AnyAsync(item => item.Id == id, ct))
                throw new GeographyValidationException("Choose a region");
            return new County(id, request.Name!);
        });
        MapNames<Town>(group, "towns", async (request, db, ct) =>
        {
            if (request.ParentId is not Guid id || !await db.Counties.AnyAsync(item => item.Id == id, ct))
                throw new GeographyValidationException("Choose a county");
            return new Town(id, request.Name!);
        });
        group.MapPost("/import", (HttpRequest request, GeographyStore store, CancellationToken ct) => GuardAsync(async () =>
        {
            if (!string.Equals(request.ContentType?.Split(';')[0].Trim(), "text/csv", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new GeographyError("Upload a UTF-8 CSV file."));
            var size = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = GeographyImportLimits.MaximumBytes;
            if (request.ContentLength > GeographyImportLimits.MaximumBytes)
                return Results.BadRequest(new GeographyError("The CSV file must be 1 MiB or smaller."));
            byte[] bytes = new byte[GeographyImportLimits.MaximumBytes + 1];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = await request.Body.ReadAsync(bytes.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > GeographyImportLimits.MaximumBytes)
                return Results.BadRequest(new GeographyError("The CSV file must be 1 MiB or smaller."));
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes, 0, count); }
            catch (DecoderFallbackException) { return Results.BadRequest(new GeographyError("Upload a UTF-8 CSV file.")); }
            return Results.Ok(await store.ImportAsync(GeographyCsv.Parse(text), ct));
        }));
    }

    private static void MapNames<T>(RouteGroupBuilder group, string level,
        Func<CreateGeographyRequest, DirectoryDbContext, CancellationToken, Task<T>> create) where T : GeographyEntity
    {
        group.MapGet($"/{level}/{{id:guid}}", (Guid id, GeographyStore store, CancellationToken ct) => GuardAsync(async () =>
            await store.FindAsync<T>(id, level, ct) is { } item ? Results.Ok(item) : Results.NotFound()));
        group.MapPost($"/{level}", (CreateGeographyRequest request, DirectoryDbContext db, GeographyStore store, CancellationToken ct) =>
            GuardAsync(() => GeographyTransactions.RunAsync<IResult>(db, async () =>
        {
            T entity = await create(request, db, ct);
            await store.EnsureActiveParentAsync(level, request.ParentId, ct);
            db.Set<T>().Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/directory/geography/{level}/{entity.Id}", ToItem(entity));
        }, ct)));
        group.MapPut($"/{level}/{{id:guid}}/name", (Guid id, RenameGeographyRequest request, DirectoryDbContext db, CancellationToken ct) => GuardAsync(async () =>
        {
            T? entity = await db.Set<T>().SingleOrDefaultAsync(item => item.Id == id, ct);
            if (entity is null) return Results.NotFound();
            byte[] version;
            try { version = Convert.FromBase64String(request.Version ?? string.Empty); }
            catch (FormatException) { throw new GeographyValidationException("Reload this page before renaming."); }
            if (version.Length != 8) throw new GeographyValidationException("Reload this page before renaming.");
            db.Entry(entity).Property(item => item.Version).OriginalValue = version;
            entity.Rename(request.Name!);
            // Even an unchanged name must check the submitted version.
            db.Entry(entity).Property(item => item.Name).IsModified = true;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToItem(entity));
        }));
        group.MapPost($"/{level}/{{id:guid}}/retire", (Guid id, RetireGeographyRequest request, GeographyStore store, CancellationToken ct) => GuardAsync(async () =>
        {
            if (!Enum.IsDefined(request.Action)) return Results.BadRequest(new GeographyError("Choose a valid action."));
            return await store.RetireAsync<T>(id, level, request, ct) switch
            {
                ReferenceMutationStatus.Saved => Results.Ok(new GeographyMutationResult(true)),
                ReferenceMutationStatus.Missing => Results.NotFound(),
                _ => RetirementConflict()
            };
        }));
    }

    private static GeographyItem ToItem(GeographyEntity entity) => GeographyStore.Item(entity,
        entity switch { County county => county.RegionId, Town town => town.CountyId, _ => null });

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (GeographyValidationException exception) { return Results.BadRequest(new GeographyError(exception.Message)); }
        catch (ReferenceUsageUnavailableException) { return Results.StatusCode(503); }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        { return Results.BadRequest(new GeographyError("The CSV file must be 1 MiB or smaller.")); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new GeographyError("This record changed. Reload before renaming.")); }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        { return Results.Conflict(new GeographyError("A name like this already exists here. No changes were saved.")); }
        // EF can wrap deadlocks in InvalidOperationException when a provider has no retry policy.
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 547 or 1205 })
        { return RetirementConflict(); }
    }

    private static IResult RetirementConflict() => Results.Conflict(new GeographyError(
        "This place changed or is now in use. Reload it before continuing."));
}
