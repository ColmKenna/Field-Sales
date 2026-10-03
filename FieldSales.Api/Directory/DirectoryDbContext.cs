using FieldSales.Api.Coverage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Api.Directory;

public sealed class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options)
{
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<County> Counties => Set<County>();
    public DbSet<Town> Towns => Set<Town>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationType> LocationTypes => Set<LocationType>();
    public DbSet<ContactType> ContactTypes => Set<ContactType>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<LocationContact> LocationContacts => Set<LocationContact>();
    public DbSet<LocationPositionHistory> LocationPositionHistory => Set<LocationPositionHistory>();
    public DbSet<TerritoryAssignment> TerritoryAssignments => Set<TerritoryAssignment>();
    public DbSet<RepReportingLine> RepReportingLines => Set<RepReportingLine>();
    public DbSet<AssignmentHistory> AssignmentHistory => Set<AssignmentHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<GeographyEntity>();
        modelBuilder.Ignore<FieldSales.Api.Catalogue.NamedReferenceItem>();
        modelBuilder.Ignore<DirectoryType>();
        ContactModelConfiguration.Configure(modelBuilder);
        LocationPositionModelConfiguration.Configure(modelBuilder);
        CoverageModelConfiguration.Configure(modelBuilder);
        AssignmentHistoryModelConfiguration.Configure(modelBuilder);
        ConfigureType<LocationType>(modelBuilder, "LocationTypes");
        ConfigureType<ContactType>(modelBuilder, "ContactTypes");
        var regions = Configure<Region>(modelBuilder, "Regions");
        regions.HasIndex(item => item.NormalizedName).IsUnique();
        var counties = Configure<County>(modelBuilder, "Counties");
        counties.HasIndex(item => new { item.RegionId, item.NormalizedName }).IsUnique();
        counties.HasOne<Region>().WithMany().HasForeignKey(item => item.RegionId).OnDelete(DeleteBehavior.Restrict);
        var towns = Configure<Town>(modelBuilder, "Towns");
        towns.Property(item => item.Latitude).HasPrecision(10, 7);
        towns.Property(item => item.Longitude).HasPrecision(10, 7);
        towns.ToTable("Towns", table => table.HasCheckConstraint("CK_Towns_Coordinates", """
            ([Latitude] IS NULL AND [Longitude] IS NULL) OR
            ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL
              AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)
            """.ReplaceLineEndings("\n")));
        towns.HasIndex(item => new { item.CountyId, item.NormalizedName }).IsUnique();
        towns.HasOne<County>().WithMany().HasForeignKey(item => item.CountyId).OnDelete(DeleteBehavior.Restrict);

        var customers = modelBuilder.Entity<Customer>();
        customers.ToTable("Customers");
        customers.HasKey(item => item.Id);
        customers.Property(item => item.Id).ValueGeneratedNever();
        customers.Property(item => item.Name).HasMaxLength(CustomerDirectoryFields.MaximumNameLength).IsRequired();
        customers.Property(item => item.Version).IsRowVersion();
        customers.HasMany(item => item.Locations).WithOne().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        customers.Navigation(item => item.Locations).HasField("_locations").UsePropertyAccessMode(PropertyAccessMode.Field);

        var locations = modelBuilder.Entity<Location>();
        locations.ToTable("Locations");
        locations.HasKey(item => item.Id);
        locations.Property(item => item.Id).ValueGeneratedNever();
        locations.Property(item => item.Name).HasMaxLength(CustomerDirectoryFields.MaximumNameLength).IsRequired();
        locations.Property(item => item.NormalizedName).HasMaxLength(CustomerDirectoryFields.MaximumNameLength)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        locations.Property(item => item.Eircode).HasMaxLength(Location.MaximumEircodeLength);
        locations.Property(item => item.Version).IsRowVersion();
        locations.HasIndex(item => new { item.CustomerId, item.NormalizedName });
        locations.HasOne<Town>().WithMany().HasForeignKey(item => item.TownId).OnDelete(DeleteBehavior.Restrict);
        locations.HasOne<LocationType>().WithMany().HasForeignKey(item => item.LocationTypeId).OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardHistory()
    {
        if (ChangeTracker.Entries<AssignmentHistory>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Assignment history is append-only.");
    }

    private static void ConfigureType<T>(ModelBuilder modelBuilder, string table) where T : DirectoryType
    {
        var entity = modelBuilder.Entity<T>(); entity.HasBaseType((Type?)null); entity.ToTable(table);
        entity.HasKey(item => item.Id); entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.Name).HasMaxLength(200).UseCollation("Latin1_General_100_CI_AS").IsRequired();
        entity.HasIndex(item => item.Name).IsUnique(); entity.Property(item => item.Description).HasMaxLength(2000);
        entity.Property(item => item.Version).IsRowVersion();
    }

    private static EntityTypeBuilder<T> Configure<T>(ModelBuilder modelBuilder, string table) where T : GeographyEntity
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasBaseType((Type?)null);
        entity.ToTable(table);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
        entity.Property(item => item.NormalizedName).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2").IsRequired();
        entity.Property(item => item.Version).IsRowVersion();
        return entity;
    }
}

public sealed class DirectoryDbContextFactory : IDesignTimeDbContextFactory<DirectoryDbContext>
{
    public DirectoryDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DirectoryDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DirectoryDb_DesignTime;Trusted_Connection=True").Options);
}
