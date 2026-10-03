using System.Security.Claims;
using FieldSales.Directory.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public static class CoverageEndpoints
{
    public static void MapCoverageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/coverage").RequireAuthorization("ManageCoverage");
        group.MapGet("/reps/{repSubject}/territory", (string repSubject, ClaimsPrincipal user, RepTerritoryReader store, CancellationToken ct) =>
            GuardAsync(async () => Results.Ok(await store.ReadAsync(user, repSubject, ct))));
        group.MapGet("/reps", (ClaimsPrincipal user, ReportingLineStore store, CancellationToken ct) =>
            GuardAsync(async () => Results.Ok(await store.RepsAsync(user, ct))));
        group.MapGet("/assignment-options", (string? repSubject, ClaimsPrincipal user, AssignmentReviewStore store, CancellationToken ct) =>
            GuardAsync(async () => Results.Ok(await store.OptionsAsync(user, repSubject, ct))));
        group.MapPost("/assignments/preview", (AddTerritoryAssignmentRequest request, ClaimsPrincipal user,
            TerritoryAssignmentStore store, CancellationToken ct) => GuardAsync(async () =>
                Results.Ok(await store.PreviewAddAsync(user, request, ct))));
        group.MapPost("/assignments/{id:guid}/remove/preview", (Guid id, RemoveTerritoryAssignmentRequest request, ClaimsPrincipal user,
            TerritoryAssignmentStore store, CancellationToken ct) => GuardAsync(async () =>
                await store.PreviewRemoveAsync(user, id, request, ct) is { } preview ? Results.Ok(preview) : Results.NotFound()));
        group.MapPost("/assignments", (AddTerritoryAssignmentRequest request, ClaimsPrincipal user,
            TerritoryAssignmentStore store, CancellationToken ct) => GuardAsync(async () =>
            {
                var saved = await store.AddAsync(user, request, ct);
                return Results.Created($"/coverage/reps/{Uri.EscapeDataString(saved.RepSubject)}/assignments", saved);
            }));
        group.MapPost("/assignments/{id:guid}/remove", (Guid id, RemoveTerritoryAssignmentRequest request, ClaimsPrincipal user,
            TerritoryAssignmentStore store, CancellationToken ct) => GuardAsync(async () =>
                await store.RemoveAsync(user, id, request, ct) is { } saved ? Results.Ok(saved) : Results.NotFound()));
        group.MapGet("/reporting-lines", (ClaimsPrincipal user, ReportingLineStore store, CancellationToken ct) =>
            GuardAsync(async () => Results.Ok(await store.ListAsync(user, ct)))).RequireAuthorization("HeadOfficeDirectory");
        group.MapPut("/reporting-lines/{repSubject}", (string repSubject, SetRepReportingLineRequest request,
            ClaimsPrincipal user, ReportingLineStore store, CancellationToken ct) =>
            GuardAsync(async () => Results.Ok(await store.SetAsync(user, repSubject, request, ct)))).RequireAuthorization("HeadOfficeDirectory");
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
        catch (CoverageValidationException exception) { return Results.BadRequest(new CoverageError(exception.Message, exception.Field)); }
        catch (CoveragePreviewChangedException exception) { return Results.Conflict(new CoverageError(exception.Message, Preview: exception.Preview)); }
        catch (CoverageConflictException exception) { return Results.Conflict(new CoverageError(exception.Message)); }
        catch (CoverageIdentityUnavailableException exception)
        { return Results.Json(new CoverageError(exception.Message), statusCode: StatusCodes.Status503ServiceUnavailable); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new CoverageError("This item changed. Reload it before saving.", "Version")); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 or 547 or 1205 })
        { return Results.Conflict(new CoverageError("This change conflicts with another saved change. Reload before saving.")); }
    }
}
