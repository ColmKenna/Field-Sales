using System.Net;
using System.Net.Http.Json;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;

// A process the tests own and kill at a recovery boundary. It never runs outside the test suite.
// Usage: <root> <rep> apply <gzip> <gzipSha256> <plainSha256> <boundary> <readyFile>
//        <root> <rep> purge <boundary> <readyFile>
//        <root> <rep> upload <boundary> <readyFile>   (its server confirms every item)
var workspace = new RepWorkspace(args[0], args[1]);
string boundary = args[2] == "apply" ? args[6] : args[3];
string ready = args[2] == "apply" ? args[7] : args[4];
var hooks = new SnapshotHooks
{
    AtBoundary = reached =>
    {
        if (reached != boundary) return;
        File.WriteAllText(ready, reached);
        Thread.Sleep(Timeout.Infinite);
    }
};
if (args[2] == "upload")
{
    using HttpClient http = new(new ConfirmingServer()) { BaseAddress = new Uri("https://api.test/") };
    await new WorkUploader(workspace, http, TimeProvider.System).UploadAsync("token", hooks, null, CancellationToken.None);
    return 0;
}
var store = new SnapshotStore(workspace);
if (args[2] == "apply") await store.ApplyAsync(args[3], new ExpectedSnapshot(args[4], args[5]), hooks);
else await store.PurgeRetiredAsync(hooks);
return 0;

internal sealed class ConfirmingServer : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new UploadReceipt(Guid.Parse(request.RequestUri!.Segments[^1]), UploadReceiptStatus.Received),
                UploadJsonContext.Default.UploadReceipt)
        });
}

public partial class Program;
