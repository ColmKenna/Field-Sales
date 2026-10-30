using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldSales.Snapshot.Contracts;

/// <summary>How the tablet sends its finished work at Sync (T-4.1.2), shared by both sides of the Sync contract.</summary>
/// <remarks>
/// Each item goes alone, with a <c>PUT</c> to its own path named by the id the tablet gave it at capture. The body is the
/// item exactly as the tablet stored it. The server answers <see cref="UploadReceiptStatus.Received"/> (201) the first time
/// and <see cref="UploadReceiptStatus.AlreadyReceived"/> (200) for every re-send, so a lost confirmation is recovered by
/// sending again; either answer means the tablet marks the item Sent. A re-send never creates a second record, and the
/// first version received is the one kept. This contract does not change the snapshot document or its version.
/// </remarks>
public static class TabletUploadContract
{
    public const string OrdersPath = "/tablet/uploads/orders";
    public const string CallsPath = "/tablet/uploads/calls";
    public const string MediaType = "application/json";

    /// <summary>The largest item body the server reads. A larger item is refused unread.</summary>
    public const int MaximumBodyBytes = 1024 * 1024;

    public static string OrderPath(Guid id) => $"{OrdersPath}/{id:D}";
    public static string CallPath(Guid id) => $"{CallsPath}/{id:D}";

    public static JsonSerializerOptions JsonOptions => UploadJsonContext.Default.Options;
}

[JsonConverter(typeof(JsonStringEnumConverter<UploadReceiptStatus>))]
public enum UploadReceiptStatus { Received, AlreadyReceived }

/// <summary>The server's confirmation that it holds the item with this id.</summary>
public sealed record UploadReceipt(Guid Id, UploadReceiptStatus Status);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(UploadReceipt))]
public sealed partial class UploadJsonContext : JsonSerializerContext;
