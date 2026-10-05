// Secrets required before `dotnet run` will succeed. Run these from the FieldSales.AppHost directory:
//
//   dotnet user-secrets set "Parameters:sql-password"            "<a strong SQL Server SA password>"
//   dotnet user-secrets set "Parameters:staff-web-client-secret" "<a random secret string>"
//   dotnet user-secrets set "Parameters:seed-sysadmin-password"  "<a strong password>"
//   dotnet user-secrets set "Parameters:seed-test-user-password" "<a strong password>"

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Projects;
using FieldSales.DemoData;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

bool demo = builder.Configuration.GetValue<bool>("Demo:Enabled");
if (demo && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("The demo profile is only available in Development.");
int? sqlPort = demo ? DemoProfile.SqlPort : builder.Configuration.GetValue<int?>("SqlServer:Port");

if (string.IsNullOrEmpty(builder.Configuration["Parameters:sql-password"]))
    throw new InvalidOperationException(
        "Missing required secret 'Parameters:sql-password'. Set it with: " +
        "dotnet user-secrets set \"Parameters:sql-password\" \"<password>\" (run from FieldSales.AppHost).");

IResourceBuilder<ParameterResource> sqlPassword = builder.AddParameter("sql-password", true);
IResourceBuilder<ParameterResource> staffWebClientSecret = builder.AddParameter("staff-web-client-secret", true);
IResourceBuilder<ParameterResource> sysAdminPassword = builder.AddParameter("seed-sysadmin-password", true);
IResourceBuilder<ParameterResource> testUserPassword = builder.AddParameter("seed-test-user-password", true);

IResourceBuilder<SqlServerServerResource> sqlServer = builder.AddSqlServer(demo ? "demo-sqlserver" : "sqlserver", sqlPassword, sqlPort)
    .WithDataVolume(demo ? DemoProfile.VolumeName : "fieldsales-identity-sqlserver-data");

string DatabaseName(string name) => demo ? DemoProfile.DatabaseName(name) : name;
IResourceBuilder<SqlServerDatabaseResource> identityDb = sqlServer.AddDatabase("IdentityDb", DatabaseName("IdentityDb"));
IResourceBuilder<SqlServerDatabaseResource> identityConfigDb = sqlServer.AddDatabase("IdentityConfigDb", DatabaseName("IdentityConfigDb"));
IResourceBuilder<SqlServerDatabaseResource> identityOperationalDb = sqlServer.AddDatabase("IdentityOperationalDb", DatabaseName("IdentityOperationalDb"));
IResourceBuilder<SqlServerDatabaseResource> staffWebDb = sqlServer.AddDatabase("StaffWebDb", DatabaseName("StaffWebDb"));
IResourceBuilder<SqlServerDatabaseResource> catalogueDb = sqlServer.AddDatabase("CatalogueDb", DatabaseName("CatalogueDb"));
IResourceBuilder<SqlServerDatabaseResource> directoryDb = sqlServer.AddDatabase("DirectoryDb", DatabaseName("DirectoryDb"));

IResourceBuilder<ProjectResource> identityServer = builder.AddProject<FieldSales_Identity>("identityserver")
    .WithReference(identityDb)
    .WithReference(identityConfigDb)
    .WithReference(identityOperationalDb)
    .WaitFor(sqlServer)
    .WithHttpsEndpoint(7201, name: "https")
    .WithEnvironment("Authentication__Authority", "https://localhost:7201")
    .WithEnvironment("Clients__StaffWebUri", "https://localhost:7203")
    .WithEnvironment("Clients__StaffWebSecret", staffWebClientSecret)
    .WithEnvironment("Seed__SysAdminPassword", sysAdminPassword)
    .WithEnvironment("Seed__TestUserPassword", testUserPassword);

IResourceBuilder<ProjectResource> staffApi = builder.AddProject<FieldSales_Api>("staff-api")
    .WithReference(catalogueDb)
    .WithReference(directoryDb)
    .WithHttpsEndpoint(7204, name: "https")
    .WithEnvironment("Authentication__Authority", "https://localhost:7201")
    .WaitFor(identityServer);

builder.AddProject<FieldSales_Web>("staff-web")
    .WithReference(staffWebDb)
    .WithHttpsEndpoint(7203, name: "https")
    .WithEnvironment("Authentication__Authority", "https://localhost:7201")
    .WithEnvironment("Authentication__ClientSecret", staffWebClientSecret)
    .WithEnvironment("StaffApi__BaseUrl", "https://localhost:7204")
    .WaitFor(identityServer)
    .WaitFor(staffApi)
    .WaitFor(sqlServer);

builder.Build().Run();
