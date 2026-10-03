using System.Security.Claims;

namespace FieldSales.Api.Coverage;

public static class CoverageEndpoints
{
    public static void MapCoverageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/coverage").RequireAuthorization("ManageCoverage");
        group.MapGet("/reps/{repSubject}/assignments", (string repSubject, ClaimsPrincipal user,
            CoverageReadStore store, CancellationToken ct) => GuardAsync(async () =>
                Results.Ok(await store.ListAssignmentsAsync(user, repSubject, ct))));
        group.MapGet("/reps/{repSubject}/locations", (string repSubject, ClaimsPrincipal user,
            CoverageReadStore store, CancellationToken ct) => GuardAsync(async () =>
                Results.Ok(await store.ListLocationsAsync(user, repSubject, ct))));
        group.MapGet("/locations/{id:guid}/owner", (Guid id, ClaimsPrincipal user,
            CoverageReadStore store, CancellationToken ct) => GuardAsync(async () =>
                await store.FindLocationAsync(user, id, ct) is { } location
                    ? Results.Ok(location) : Results.NotFound()));
    }

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CoverageReadForbiddenException) { return Results.Forbid(); }
    }
}
