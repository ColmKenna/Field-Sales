using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Identity.Data;
using FieldSales.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.DemoData;

internal static class DemoCommand
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || args[0] is not ("seed" or "status"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tools/FieldSales.DemoData -- <seed|status>");
            Console.Error.WriteLine("Start and reset the isolated environment with scripts/demo-data.sh.");
            return 2;
        }
        string? environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment is not (null or "Development"))
        {
            Console.Error.WriteLine("Demo commands are only available in Development.");
            return 2;
        }
        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(DemoCommand).Assembly, optional: true)
                .AddEnvironmentVariables().Build();
            string sqlPassword = Required(configuration, "Parameters:sql-password");
            string? staffPassword = args[0] == "seed"
                ? Required(configuration, "Parameters:seed-test-user-password") : null;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Connection("IdentityDb", sqlPassword)));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton(new ConfigurationStoreOptions());
            services.AddSingleton(new OperationalStoreOptions());
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var catalogue = new CatalogueDbContext(Options<CatalogueDbContext>("CatalogueDb", sqlPassword));
            await using var directory = new DirectoryDbContext(Options<DirectoryDbContext>("DirectoryDb", sqlPassword));
            await using var web = new StaffWebDbContext(Options<StaffWebDbContext>("StaffWebDb", sqlPassword));
            await using var config = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseApplicationServiceProvider(provider).UseSqlServer(Connection("IdentityConfigDb", sqlPassword),
                    options => options.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)).Options);
            await using var grants = new PersistedGrantDbContext(new DbContextOptionsBuilder<PersistedGrantDbContext>()
                .UseApplicationServiceProvider(provider).UseSqlServer(Connection("IdentityOperationalDb", sqlPassword),
                    options => options.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)).Options);
            // Read-only preflight across all six databases before changing anything.
            foreach (DbContext db in new DbContext[] { identity, config, grants, web, catalogue, directory })
            {
                if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
                    throw new InvalidOperationException($"{db.Database.GetDbConnection().Database} is not ready. Start the demo profile and wait for all three apps to be healthy, then retry.");
            }
            Console.WriteLine($"Demo SQL: 127.0.0.1:{DemoProfile.SqlPort}; seed: {DemoSeedBatch.SeedName}");
            if (args[0] == "seed")
            {
                Report("Staff", await DemoStaffSeeder.SeedAsync(identity,
                    scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                    scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(), staffPassword!));
                Report("Catalogue", await DemoScenarioSeeder.SeedCatalogueAsync(catalogue));
                Report("Directory and coverage", await DemoScenarioSeeder.SeedDirectoryAsync(directory));
            }
            foreach (var (name, db) in new (string, DbContext)[] { ("Staff", identity), ("Catalogue", catalogue), ("Directory", directory) })
                Console.WriteLine($"{name}: {(await DemoSeedBatch.IsSeededAsync(db) ? "seeded" : "not seeded")}");
            Console.WriteLine($"Catalogue: {await catalogue.Categories.CountAsync()} categories, {await catalogue.Products.CountAsync()} products, {await catalogue.Brands.CountAsync()} brands");
            Console.WriteLine($"Directory: {await directory.Customers.CountAsync()} customers, {await directory.Locations.CountAsync()} locations, {await directory.Contacts.CountAsync()} contacts");
            Console.WriteLine($"Coverage: {await directory.TerritoryAssignments.CountAsync()} assignments, {await directory.AssignmentHistory.CountAsync()} history entries");
            foreach (var staff in DemoStaffSeeder.Staff)
            {
                var user = await identity.Users.SingleOrDefaultAsync(item => item.Id == staff.Subject);
                string state = user is null ? "not created" : string.Join(", ",
                    await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));
                Console.WriteLine($"{DemoStaffSeeder.Email(staff.Subject)} — {(state.Length == 0 ? "no roles" : state)}");
            }
            Console.WriteLine("Demo staff passwords use Parameters:seed-test-user-password from AppHost user secrets.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Demo command failed: " + exception.Message);
            Console.Error.WriteLine("Start scripts/demo-data.sh start and wait for the apps to be healthy. Completed seed batches remain safe to retry.");
            return 1;
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]!
            : throw new InvalidOperationException($"Missing {key}. Configure user secrets on src/FieldSales.AppHost.");

    private static void Report(string name, bool created) =>
        Console.WriteLine($"{name}: {(created ? "sample data created" : "already seeded; existing data preserved")}");

    // No arbitrary server or database override: the command can only reach the local
    // demo endpoint and the six explicitly named demo databases.
    private static string Connection(string resourceName, string password) => new SqlConnectionStringBuilder
    {
        DataSource = $"127.0.0.1,{DemoProfile.SqlPort}",
        InitialCatalog = DemoProfile.DatabaseName(resourceName),
        UserID = "sa", Password = password, TrustServerCertificate = true, ConnectTimeout = 5
    }.ConnectionString;

    private static DbContextOptions<T> Options<T>(string name, string password) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer(Connection(name, password)).Options;
}
