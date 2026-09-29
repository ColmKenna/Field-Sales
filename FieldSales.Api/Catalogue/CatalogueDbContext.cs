using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name)
                .HasMaxLength(Category.MaximumNameLength)
                .UseCollation("Latin1_General_100_CI_AS")
                .IsRequired();
            entity.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(category => category.Name)
                .IsUnique().HasDatabaseName("UX_Categories_RootName")
                .HasFilter("[ParentId] IS NULL");
            entity.HasIndex(category => new { category.ParentId, category.Name })
                .IsUnique().HasDatabaseName("UX_Categories_SiblingName")
                .HasFilter("[ParentId] IS NOT NULL");
        });
    }
}
