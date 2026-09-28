// Secrets required before `dotnet run` will succeed. Run these from the FieldSales.AppHost directory:
//
//   dotnet user-secrets set "Parameters:sql-password"            "<a strong SQL Server SA password>"
//   dotnet user-secrets set "Parameters:staff-web-client-secret" "<a random secret string>"
//   dotnet user-secrets set "Parameters:seed-sysadmin-password"  "<a strong password>"
//   dotnet user-secrets set "Parameters:seed-test-user-password" "<a strong password>"

using Microsoft.Extensions.Configuration;
using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

int? sqlPort = builder.Configuration.GetValue<int?>("SqlServer:Port");

if (string.IsNullOrEmpty(builder.Configuration["Parameters:sql-password"]))
    throw new InvalidOperationException(
        "Missing required secret 'Parameters:sql-password'. Set it with: " +
        "dotnet user-secrets set \"Parameters:sql-password\" \"<password>\" (run from FieldSales.AppHost).");

IResourceBuilder<ParameterResource> sqlPassword = builder.AddParameter("sql-password", true);
IResourceBuilder<ParameterResource> staffWebClientSecret = builder.AddParameter("staff-web-client-secret", true);
IResourceBuilder<ParameterResource> sysAdminPassword = builder.AddParameter("seed-sysadmin-password", true);
IResourceBuilder<ParameterResource> testUserPassword = builder.AddParameter("seed-test-user-password", true);

IResourceBuilder<SqlServerServerResource> sqlServer = builder.AddSqlServer("sqlserver", sqlPassword, sqlPort)
    .WithDataVolume("fieldsales-identity-sqlserver-data");

IResourceBuilder<SqlServerDatabaseResource> identityDb = sqlServer.AddDatabase("IdentityDb");
IResourceBuilder<SqlServerDatabaseResource> identityConfigDb = sqlServer.AddDatabase("IdentityConfigDb");
IResourceBuilder<SqlServerDatabaseResource> identityOperationalDb = sqlServer.AddDatabase("IdentityOperationalDb");

IResourceBuilder<ProjectResource> identityServer = builder.AddProject<FieldSales_Identity>("identityserver")
    .WithReference(identityDb)
    .WithReference(identityConfigDb)
    .WithReference(identityOperationalDb)
    .WaitFor(sqlServer)
    .WithHttpsEndpoint(7201, name: "https")
    .WithEnvironment("Clients__StaffWebUri", "https://localhost:7203")
    .WithEnvironment("Clients__StaffWebSecret", staffWebClientSecret)
    .WithEnvironment("Seed__SysAdminPassword", sysAdminPassword)
    .WithEnvironment("Seed__TestUserPassword", testUserPassword);

builder.Build().Run();
