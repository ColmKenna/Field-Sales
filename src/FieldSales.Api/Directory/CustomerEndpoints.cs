using FieldSales.Directory.Contracts;
using FieldSales.Api.Coverage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/directory").RequireAuthorization("HeadOfficeDirectory");
        group.MapGet("/customers", async (CustomerStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct)));
        group.MapGet("/customers/{id:guid}", async (Guid id, CustomerStore store, CancellationToken ct) =>
            await store.FindCustomerAsync(id, ct) is { } customer ? Results.Ok(customer) : Results.NotFound());
        group.MapPost("/customers", (CreateCustomerRequest request, CustomerStore store, CancellationToken ct) => GuardAsync(async () =>
        {
            var customer = await store.CreateAsync(request, ct);
            return Results.Created($"/directory/customers/{customer.Id}", customer);
        }));
        group.MapPost("/customers/{id:guid}/locations", (Guid id, CreateLocationRequest request, CustomerStore store, CancellationToken ct) =>
            GuardAsync(async () => await store.AddLocationAsync(id, request, ct) is { } location
                ? Results.Created($"/directory/locations/{location.Id}", location) : Results.NotFound()));
        group.MapGet("/locations/{id:guid}", async (Guid id, CustomerStore store, CancellationToken ct) =>
            await store.FindLocationAsync(id, ct) is { } location ? Results.Ok(location) : Results.NotFound());
        group.MapPut("/locations/{id:guid}", (Guid id, EditLocationRequest request, CustomerStore store, CancellationToken ct) =>
            GuardAsync(async () => await store.EditLocationAsync(id, request, ct) is { } location ? Results.Ok(location) : Results.NotFound()));
    }

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CustomerDirectoryValidationException exception)
        { return Results.BadRequest(new CustomerDirectoryError(exception.Message, exception.Field)); }
        catch (DuplicateLocationNameException exception)
        { return Results.Conflict(new CustomerDirectoryError(exception.Message, "Name", RequiresDuplicateConfirmation: true)); }
        catch (CoverageIdentityUnavailableException exception)
        { return Results.Json(new CustomerDirectoryError(exception.Message), statusCode: StatusCodes.Status503ServiceUnavailable); }
        catch (DbUpdateConcurrencyException)
        { return Results.Conflict(new CustomerDirectoryError("This location changed. Reload it before saving.", "Version")); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 547 or 1205 })
        { return Results.Conflict(new CustomerDirectoryError("This location or its town changed. Reload before saving.")); }
    }
}
