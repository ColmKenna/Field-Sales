using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Api.TabletSync;

public static class TabletSyncEndpoints
{
    /// <summary>An active Field Salesperson with the Field Sales API scope; roles are refreshed per request.</summary>
    public const string Policy = "TabletSync";

    public static void MapTabletSyncEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet(SnapshotContract.Path, GetSnapshotAsync).RequireAuthorization(Policy);

    // The snapshot is always the caller's own; no request field can name another rep.
    private static async Task<IResult> GetSnapshotAsync(HttpContext http, TabletSnapshotBuilder builder, CancellationToken ct)
    {
        IReadOnlyList<int>? accepted = SnapshotContract.ParseAcceptedMajors(
            http.Request.Headers[SnapshotContract.AcceptedMajorsHeader].ToString());
        if (accepted is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [SnapshotContract.AcceptedMajorsHeader] = ["Send the snapshot versions this app reads."]
            });
        if (SnapshotContract.Negotiate(accepted, SnapshotContract.ServedMajors) is null)
            return Results.Problem(title: SnapshotContract.UpdateRequiredTitle,
                detail: $"This server builds snapshot version {string.Join(", ", SnapshotContract.ServedMajors)}.",
                statusCode: StatusCodes.Status406NotAcceptable);

        TabletSnapshot snapshot = await builder.BuildAsync(http.User.GetStaffSubject()!, ct);
        // Built completely before the response starts, so a failure never sends part of a snapshot.
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotContract.JsonOptions);
        byte[] gzip = Compress(plain);
        IHeaderDictionary headers = http.Response.Headers;
        headers[SnapshotContract.VersionHeader] = snapshot.Manifest.Version;
        headers[SnapshotContract.GzipSha256Header] = Convert.ToHexStringLower(SHA256.HashData(gzip));
        headers[SnapshotContract.PlainSha256Header] = Convert.ToHexStringLower(SHA256.HashData(plain));
        headers.CacheControl = "no-store";
        return Results.File(gzip, SnapshotContract.MediaType);
    }

    private static byte[] Compress(byte[] plain)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(plain);
        return output.ToArray();
    }
}
