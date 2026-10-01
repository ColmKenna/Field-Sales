using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>Common single-database host workflow; provider-specific migration operations stay in the host.</summary>
public static class DatabaseStartup
{
    public static async Task EnsureSchemaAsync<TContext>(IServiceProvider services, IHostEnvironment environment,
        string databaseName, Func<TContext, Task> migrate, Func<TContext, Task<IEnumerable<string>>> pendingMigrations)
        where TContext : class
    {
        if (environment.IsEnvironment("Testing")) return;
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        if (environment.IsDevelopment()) await migrate(context);
        else if ((await pendingMigrations(context)).Any())
            throw new InvalidOperationException($"{databaseName} has pending migrations.");
    }
}
