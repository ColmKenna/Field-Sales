using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Api.Ordering;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Api.TabletSync;

public static class TabletSyncEndpoints
{
    /// <summary>An active Field Salesperson with the Field Sales API scope; roles are refreshed per request.</summary>
    public const string Policy = "TabletSync";

    public static void MapTabletSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(SnapshotContract.Path, GetSnapshotAsync).RequireAuthorization(Policy);
        app.MapPut(TabletUploadContract.OrdersPath + "/{id:guid}", ReceiveOrderAsync).RequireAuthorization(Policy);
    }

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

    // An uploaded item is always the caller's own: the rep is taken from the token, never from the body.
    private static async Task<IResult> ReceiveOrderAsync(Guid id, HttpContext http, TabletWorkReceiver receiver, CancellationToken ct)
    {
        byte[]? body = await ReadBodyAsync(http.Request, ct);
        if (body is null)
            return Results.Problem(title: "This item is too large to send.", statusCode: StatusCodes.Status413PayloadTooLarge);
        return Respond(id, await receiver.ReceiveOrderAsync(id, http.User.GetStaffSubject()!, body, ct));
    }

    private static IResult Respond(Guid id, ReceiveResult result) => result.Outcome switch
    {
        ReceiveOutcome.Received => Results.Json(new UploadReceipt(id, UploadReceiptStatus.Received),
            UploadJsonContext.Default.UploadReceipt, statusCode: StatusCodes.Status201Created),
        ReceiveOutcome.AlreadyReceived => Results.Json(new UploadReceipt(id, UploadReceiptStatus.AlreadyReceived),
            UploadJsonContext.Default.UploadReceipt, statusCode: StatusCodes.Status200OK),
        ReceiveOutcome.Conflict => Results.Problem(title: "This item's id is already in use.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(title: "This item couldn't be received.", detail: result.Fault, statusCode: StatusCodes.Status400BadRequest)
    };

    // Reads at most the contract's limit; null means the item is larger.
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > TabletUploadContract.MaximumBodyBytes) return null;
        using MemoryStream body = new();
        byte[] buffer = new byte[16 * 1024];
        while (await request.Body.ReadAsync(buffer, ct) is int count and > 0)
        {
            if (body.Length + count > TabletUploadContract.MaximumBodyBytes) return null;
            body.Write(buffer, 0, count);
        }
        return body.ToArray();
    }

    private static byte[] Compress(byte[] plain)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(plain);
        return output.ToArray();
    }
}
