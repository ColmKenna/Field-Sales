using System.Reflection;
using FieldSales.Identity.Services.Users;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

/// <summary>
///     Guards the extracted-library invariant behind every persistence port in
///     <c>FieldSales.Identity.Admin.Services</c>: the library must never depend on the web host or
///     on <c>FieldSales.Identity.Data</c>. A reference either way would silently reintroduce the host
///     coupling the ports exist to remove, defeating the whole extraction.
/// </summary>
[Trait("Category", "Unit")]
public class AdminServicesDependencyDirectionTests
{
    private static readonly Assembly AdminServicesAssembly = typeof(IIdentityUserAdministrationStore).Assembly;

    [Fact]
    public void AdminServicesAssembly_DoesNotReferenceTheWebHostAssembly()
    {
        IEnumerable<string?> referencedAssemblyNames =
            AdminServicesAssembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain("FieldSales.Identity", referencedAssemblyNames);
    }

    [Fact]
    public void AdminServicesAssembly_DefinesNoTypeInTheHostDataNamespace()
    {
        var dataNamespaceTypes = AdminServicesAssembly.GetTypes()
            .Where(t => t.Namespace != null &&
                        t.Namespace.StartsWith("FieldSales.Identity.Data", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(dataNamespaceTypes);
    }
}