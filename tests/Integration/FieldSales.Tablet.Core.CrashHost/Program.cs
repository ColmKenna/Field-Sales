using FieldSales.Tablet.Core;

// A process the tests own and kill at a recovery boundary. It never runs outside the test suite.
// Usage: <root> <rep> apply <gzip> <gzipSha256> <plainSha256> <boundary> <readyFile>
//        <root> <rep> purge <boundary> <readyFile>
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
var store = new SnapshotStore(workspace);
if (args[2] == "apply") await store.ApplyAsync(args[3], new ExpectedSnapshot(args[4], args[5]), hooks);
else await store.PurgeRetiredAsync(hooks);
return 0;

public partial class Program;
