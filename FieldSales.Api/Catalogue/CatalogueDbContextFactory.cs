using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Api.Catalogue;

public sealed class CatalogueDbContextFactory : IDesignTimeDbContextFactory<CatalogueDbContext>
{
    public CatalogueDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CatalogueDb_DesignTime;Trusted_Connection=True")
            .Options;
        return new CatalogueDbContext(options);
    }
}
