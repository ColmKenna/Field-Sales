using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.Apis;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Scopes;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Data;
using FieldSales.Identity.Admin.Tests.Infrastructure;
using FieldSales.Identity.Data;
using FieldSales.Identity.Services.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Identity.Admin.Tests.Task02;

[Collection(Task02SqlServerCollection.Name)]
public class TransactionHelperSqlServerTests(Task02SqlServerFactory factory)
{
    [Fact]
    public async Task ScopeCreationRetryDoesNotDuplicateScopesLinksOrAuditEvents()
    {
        string name = $"resource-{Guid.NewGuid():N}";
        string scopeName = $"scope-{Guid.NewGuid():N}";
        await factory.RunInScopeAsync(async services =>
        {
            var failure = new FailFirstScopeSave(scopeName);
            await using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseApplicationServiceProvider(services)
                .UseSqlServer(factory.ConfigurationConnectionString, sql => sql.EnableRetryOnFailure(2, TimeSpan.Zero, null))
                .AddInterceptors(failure).Options);
            db.ApiResources.Add(new ApiResource { Name = name });
            await db.SaveChangesAsync();
            var events = new List<AdminAuditEvent>();
            var writer = new Mock<IAuditWriter>();
            writer.Setup(w => w.WriteAsync(It.IsAny<AdminAuditEvent>(), It.IsAny<CancellationToken>()))
                .Callback<AdminAuditEvent, CancellationToken>((entry, _) => events.Add(entry)).Returns(Task.CompletedTask);
            var service = new ApiResourceEditorService(db, writer.Object);
            var result = await service.CreateScopeAsync(new(ScopeName.Create(name), scopeName, "Scope"));
            Assert.True(result.Succeeded);
            Assert.True(failure.Fired);
            Assert.Equal(1, await db.ApiScopes.CountAsync(scope => scope.Name == scopeName));
            var resource = await db.ApiResources.AsNoTracking().Include(resource => resource.Scopes)
                .SingleAsync(resource => resource.Name == name);
            Assert.Equal(scopeName, Assert.Single(resource.Scopes).Scope);
            Assert.Equal(AuditOutcome.Succeeded, Assert.Single(events).Outcome);
        });
    }

    private sealed class FailFirstScopeSave(string scopeName) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context!.ChangeTracker.Entries<ApiScope>().Any(entry => entry.Entity.Name == scopeName))
            {
                Fired = true;
                throw new TimeoutException("transient timeout after creating the scope and attachment");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task TransientFailureRollsBackAndRetriesWithFreshState()
    {
        string name = $"retry-{Guid.NewGuid():N}";
        await factory.RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            int attempts = 0;
            string id = await IdentityTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
            {
                Assert.Empty(db.ChangeTracker.Entries());
                var role = new IdentityRole(name);
                db.Roles.Add(role);
                await db.SaveChangesAsync();
                if (++attempts == 1) throw new TimeoutException("simulated transient timeout after save");
                await transaction.CommitAsync();
                return role.Id;
            }, default);
            Assert.Equal(2, attempts);
            Assert.Equal(id, (await db.Roles.AsNoTracking().SingleAsync(role => role.Name == name)).Id);
        });
    }

    [Fact]
    public async Task UnhandledFailureRollsBackAndPreservesException()
    {
        string name = $"rollback-{Guid.NewGuid():N}";
        var failure = new InvalidOperationException("injected failure after save");
        await factory.RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => IdentityTransactions.RunAsync<int>(
                db, IsolationLevel.Serializable, async transaction =>
                {
                    db.Roles.Add(new IdentityRole(name));
                    await db.SaveChangesAsync();
                    throw failure;
                }, default));
            Assert.Same(failure, actual);
            Assert.False(await db.Roles.AsNoTracking().AnyAsync(role => role.Name == name));
        });
    }
}
