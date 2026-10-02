using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Api.Directory;

public sealed class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options)
{
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<County> Counties => Set<County>();
    public DbSet<Town> Towns => Set<Town>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<GeographyEntity>();
        var regions = Configure<Region>(modelBuilder, "Regions");
        regions.HasIndex(item => item.NormalizedName).IsUnique();
        var counties = Configure<County>(modelBuilder, "Counties");
        counties.HasIndex(item => new { item.RegionId, item.NormalizedName }).IsUnique();
        counties.HasOne<Region>().WithMany().HasForeignKey(item => item.RegionId).OnDelete(DeleteBehavior.Restrict);
        var towns = Configure<Town>(modelBuilder, "Towns");
        towns.HasIndex(item => new { item.CountyId, item.NormalizedName }).IsUnique();
        towns.HasOne<County>().WithMany().HasForeignKey(item => item.CountyId).OnDelete(DeleteBehavior.Restrict);
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
