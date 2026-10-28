namespace FieldSales.Web.Tests;

internal static class FixtureCleanup
{
    public static async Task OnFailureAsync(Func<Task> initialize, Func<Task> cleanup)
    {
        try
        {
            await initialize();
        }
        catch (Exception setupError)
        {
            try { await cleanup(); }
            catch (Exception cleanupError)
            {
                setupError.Data["FixtureCleanupFailure"] = cleanupError;
                System.Diagnostics.Trace.TraceError("Fixture cleanup also failed: {0}", cleanupError);
            }
            throw;
        }
    }
}
