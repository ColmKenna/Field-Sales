using FieldSales.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Web.Security;

public sealed class ExpiredTicketsCleanupService(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<ExpiredTicketsCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Tickets
                    .Where(ticket => ticket.ExpiresUtc <= timeProvider.GetUtcNow())
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Expired staff tickets could not be removed.");
            }
        }
    }
}
