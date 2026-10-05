using System.Reflection;
using FieldSales.Identity.Data;
using FieldSales.Identity.Services.AuditLogs;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Admin.Tests.AuditLogs;

/// <summary>
///     TASK-04 architecture/inventory test: every mutating <c>OnPost*</c> handler under
///     <c>Pages/Admin</c> must delegate to a service method, and no PageModel may write audit
///     entries itself (services own audit events because they own the outcome and invariant).
///     This is a maintained allow-list, not a fully automatic call-graph check: adding a new
///     <c>OnPost*</c> handler that isn't in <see cref="ExpectedMutatingHandlers" /> fails this test,
///     forcing the author to confirm the delegated service method is audited (per the per-service
///     coverage tests in this project) before adding it to the list.
/// </summary>
public class PageModelAuditCoverageTests
{
    /// <summary>
    ///     (PageModel full type name, OnPost handler name) pairs confirmed to delegate to an
    ///     audited service method. Derived directly from Pages/Admin as of this task.
    /// </summary>
    private static readonly HashSet<(string TypeFullName, string Handler)> ExpectedMutatingHandlers = new()
    {
        ("FieldSales.Identity.Pages.Admin.Users.IndexModel", "OnPostUnlockAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostUnlockAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostAddRoleAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostRemoveRoleAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostAddClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostRemoveClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostRevokeUserAccessAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostSuspendAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.DetailsModel", "OnPostDeleteAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.CreateModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Users.ResetPasswordModel", "OnPostAsync"),

        ("FieldSales.Identity.Pages.Admin.Roles.CreateModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Roles.IndexModel", "OnPostDeleteAsync"),

        ("FieldSales.Identity.Pages.Admin.Grants.IndexModel", "OnPostRevokeAsync"),

        ("FieldSales.Identity.Pages.Admin.Clients.BasicsModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.DetailsModel", "OnPostToggleStatusAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.DetailsModel", "OnPostDeleteAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.PermissionsModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.TokenSettingsModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.AuthenticationModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.SecretsModel", "OnPostGenerateAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.SecretsModel", "OnPostRevokeAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.CreateModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.Clients.CloneModel", "OnPostAsync"),

        ("FieldSales.Identity.Pages.Admin.ApiScopes.EditModel", "OnPostSaveAsync"),
        ("FieldSales.Identity.Pages.Admin.ApiScopes.EditModel", "OnPostAddClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.ApiScopes.EditModel", "OnPostRemoveClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.ApiScopes.CreateModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.ApiScopes.IndexModel", "OnPostDeleteAsync"),

        ("FieldSales.Identity.Pages.Admin.IdentityResources.EditModel", "OnPostSaveAsync"),
        ("FieldSales.Identity.Pages.Admin.IdentityResources.EditModel", "OnPostAddClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.IdentityResources.EditModel", "OnPostRemoveClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.IdentityResources.CreateModel", "OnPostAsync"),
        ("FieldSales.Identity.Pages.Admin.IdentityResources.IndexModel", "OnPostDeleteAsync"),

        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostSaveBasicsAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostAddSecretAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostRevokeSecretAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostAttachScopeAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostCreateScopeAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostDetachScopeAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostAddClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostRemoveClaimAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostEnableAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostDisableAsync"),
        ("FieldSales.Identity.Pages.Admin.Apis.EditorModel", "OnPostDeleteAsync")
    };

    /// <summary>
    ///     PageModels with zero OnPost* handlers today (pure read/filter pages). Listed explicitly
    ///     so their absence from <see cref="ExpectedMutatingHandlers" /> reads as intentional.
    /// </summary>
    private static readonly HashSet<string> KnownNonMutatingPageModels = new()
    {
        "FieldSales.Identity.Pages.Admin.Clients.IndexModel",
        "FieldSales.Identity.Pages.Admin.Apis.IndexModel",
        "FieldSales.Identity.Pages.Admin.AuditLogs.IndexModel",
        "FieldSales.Identity.Pages.Admin.Diagnostics.IndexModel"
    };

    private static IEnumerable<Type> GetAdminPageModelTypes()
    {
        return typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(PageModel).IsAssignableFrom(t))
            .Where(t => t.Namespace != null &&
                        t.Namespace.StartsWith("FieldSales.Identity.Pages.Admin", StringComparison.Ordinal));
    }

    private static IEnumerable<MethodInfo> GetOnPostHandlers(Type pageModelType)
    {
        return pageModelType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("OnPost", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryOnPostHandlerUnderPagesAdmin_IsOnTheAuditedAllowList()
    {
        var actualHandlers = GetAdminPageModelTypes()
            .SelectMany(t => GetOnPostHandlers(t).Select(m => (TypeFullName: t.FullName!, Handler: m.Name)))
            .ToHashSet();

        var missingFromAllowList = actualHandlers.Except(ExpectedMutatingHandlers).ToList();
        Assert.True(missingFromAllowList.Count == 0,
            "Found OnPost* handler(s) not on the audited allow-list (add them to ExpectedMutatingHandlers " +
            "only after confirming the delegated service method audits Succeeded/Denied/Failed outcomes): " +
            string.Join(", ", missingFromAllowList.Select(h => $"{h.TypeFullName}.{h.Handler}")));

        var removedFromCode = ExpectedMutatingHandlers.Except(actualHandlers).ToList();
        Assert.True(removedFromCode.Count == 0,
            "Allow-listed handler(s) no longer exist in the codebase - remove them from ExpectedMutatingHandlers: " +
            string.Join(", ", removedFromCode.Select(h => $"{h.TypeFullName}.{h.Handler}")));
    }

    [Fact]
    public void NoPageModelUnderPagesAdmin_HasADirectIAuditWriterDependency()
    {
        // PageModels must not audit directly - only services own audit events, since only
        // services know the outcome and invariant behind a mutation.
        var offenders = GetAdminPageModelTypes()
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType == typeof(IAuditWriter)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "PageModel(s) inject IAuditWriter directly - auditing belongs in the service layer: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void KnownNonMutatingPageModels_StillHaveNoOnPostHandlers()
    {
        foreach (string typeName in KnownNonMutatingPageModels)
        {
            Type? type = GetAdminPageModelTypes().SingleOrDefault(t => t.FullName == typeName);
            Assert.True(type != null,
                $"Expected PageModel '{typeName}' was not found - update KnownNonMutatingPageModels.");

            var handlers = GetOnPostHandlers(type!).ToList();
            Assert.True(handlers.Count == 0,
                $"'{typeName}' was documented as non-mutating but now has OnPost handler(s): {string.Join(", ", handlers.Select(m => m.Name))}. " +
                "Move it into ExpectedMutatingHandlers once its delegated service method is confirmed audited.");
        }
    }
}