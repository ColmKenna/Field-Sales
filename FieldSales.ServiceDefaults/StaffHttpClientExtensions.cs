using Microsoft.Extensions.Http.Resilience;

namespace Microsoft.Extensions.DependencyInjection;

public static class StaffHttpClientExtensions
{
    public static IHttpClientBuilder AddSafeReadResilience(this IHttpClientBuilder client)
    {
        // Replace the handler inherited from ServiceDefaults so no earlier pipeline can replay writes.
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        client.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
        return client;
    }
}
