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
        group.MapGet("/locations/{id:guid}/history", (Guid id, ClaimsPrincipal user,
            AssignmentHistoryReadStore store, CancellationToken ct) => GuardAsync(async () =>
                await store.ForLocationAsync(user, id, ct) is { } entries ? Results.Ok(entries) : Results.NotFound()));
        group.MapGet("/reps/{repSubject}/history", (string repSubject, ClaimsPrincipal user,
            AssignmentHistoryReadStore store, CancellationToken ct) => GuardAsync(async () =>
                Results.Ok(await store.ForRepAsync(user, repSubject, ct))));
    }

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CoverageReadForbiddenException) { return Results.Forbid(); }
    }
}
