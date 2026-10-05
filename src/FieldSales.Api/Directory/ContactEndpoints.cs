using FieldSales.Directory.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/directory").RequireAuthorization("HeadOfficeDirectory");
        group.MapGet("/contacts/choices", (ContactStore store, CancellationToken ct) => store.ChoicesAsync(ct));
        group.MapGet("/contacts/type-choices", (ContactStore store, CancellationToken ct) => store.TypeChoicesAsync(ct));
        group.MapGet("/contacts/location-choices", (ContactStore store, CancellationToken ct) => store.LocationChoicesAsync(ct));
        group.MapGet("/contacts/{id:guid}", async (Guid id, ContactStore store, CancellationToken ct) =>
            await store.FindAsync(id, ct) is { } contact ? Results.Ok(contact) : Results.NotFound());
        group.MapPost("/contacts", (CreateContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
        {
            var contact = await store.CreateAsync(request, ct); return Results.Created($"/directory/contacts/{contact.Id}", contact);
        }));
        group.MapPut("/contacts/{id:guid}", (Guid id, EditContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
            await store.EditAsync(id, request, ct) is { } contact ? Results.Ok(contact) : Results.NotFound()));
        group.MapPost("/contacts/{id:guid}/retire", (Guid id, RetireMainContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
            await store.RetireAsync(id, request, ct) is { } result ? Results.Ok(result) : Results.NotFound()));
        group.MapPost("/locations/{locationId:guid}/contacts/{contactId:guid}/remove", (Guid locationId, Guid contactId,
            RemoveLocationContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
                await store.RemoveAsync(locationId, contactId, request, ct) is { } result ? Results.Ok(result) : Results.NotFound()));
        group.MapGet("/locations/{id:guid}/contacts", async (Guid id, bool? showInactive, ContactStore store, CancellationToken ct) =>
            await store.AtLocationAsync(id, showInactive == true, ct) is { } page ? Results.Ok(page) : Results.NotFound());
        group.MapPost("/locations/{id:guid}/contacts", (Guid id, LinkContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
            await store.LinkAsync(id, request, ct) is { } result ? Results.Ok(result) : Results.NotFound()));
        group.MapPost("/locations/{id:guid}/main-contact", (Guid id, SetMainContactRequest request, ContactStore store, CancellationToken ct) => GuardAsync(async () =>
        {
            var result = await store.SetMainAsync(id, request, ct);
            if (result is null) return Results.NotFound();
            return result.Confirmation is { } confirmation
                ? Results.Conflict(new ContactDirectoryError(confirmation.Question, Confirmation: confirmation)) : Results.Ok(result.Mutation);
        }));
    }
    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CustomerDirectoryValidationException exception) { return Results.BadRequest(new ContactDirectoryError(exception.Message, exception.Field)); }
        catch (MainContactChangedException exception) { return Results.Conflict(new ContactDirectoryError(exception.Message)); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 547 or 1205 or 2601 or 2627 }) { return Conflict(); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: >= 51001 and <= 51004 } error)
        { return Results.BadRequest(new ContactDirectoryError(error.Message)); }
    }
    private static IResult Conflict() => Results.Conflict(new ContactDirectoryError("This contact or location changed. Reload before continuing."));
}
