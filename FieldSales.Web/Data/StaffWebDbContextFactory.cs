using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Web.Data;

public sealed class StaffWebDbContextFactory : IDesignTimeDbContextFactory<StaffWebDbContext>
{
    public StaffWebDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StaffWebDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StaffWebDb_DesignTime;Trusted_Connection=True")
            .Options;
        return new StaffWebDbContext(options);
    }
}
