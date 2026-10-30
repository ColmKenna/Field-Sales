using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Api.Ordering;

/// <summary>Work received from tablets and the orders it starts (T-4.1.2). The ledger holds every received item once,
/// exactly as sent; a received order also enters the order lifecycle as Pending.</summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    private const string SubjectCollation = "Latin1_General_100_BIN2";

    public DbSet<ReceivedWork> ReceivedWork => Set<ReceivedWork>();
    public DbSet<ReceivedOrder> Orders => Set<ReceivedOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var work = modelBuilder.Entity<ReceivedWork>();
        work.ToTable("ReceivedWork", table => table.HasCheckConstraint("CK_ReceivedWork_Item", """
            [Id] <> '00000000-0000-0000-0000-000000000000' AND LEN([RepSubject]) > 0
            AND LEN([ContentSha256]) = 64 AND [ResentWithChangesCount] >= 0 AND DATEPART(TZOFFSET, [ReceivedUtc]) = 0
            """.ReplaceLineEndings("\n")));
        work.HasKey(item => item.Id);
        work.Property(item => item.Id).ValueGeneratedNever();
        work.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        work.Property(item => item.RepSubject).HasMaxLength(450).UseCollation(SubjectCollation);
        work.Property(item => item.ContentSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        work.Property(item => item.Body);

        var orders = modelBuilder.Entity<ReceivedOrder>();
        orders.ToTable("Orders", table => table.HasCheckConstraint("CK_Orders_Received", """
            [LocationId] <> '00000000-0000-0000-0000-000000000000' AND [OrderedById] <> '00000000-0000-0000-0000-000000000000'
            AND LEN([CapturingRep]) > 0 AND LEN([SnapshotVersion]) > 0 AND DATEPART(TZOFFSET, [ReceivedUtc]) = 0
            """.ReplaceLineEndings("\n")));
        orders.HasKey(item => item.Id);
        orders.Property(item => item.Id).ValueGeneratedNever();
        orders.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        orders.Property(item => item.CapturingRep).HasMaxLength(450).UseCollation(SubjectCollation);
        orders.Property(item => item.SnapshotVersion).HasMaxLength(20).IsUnicode(false);
        orders.HasOne<ReceivedWork>().WithOne().HasForeignKey<ReceivedOrder>(item => item.Id).OnDelete(DeleteBehavior.Restrict);
        orders.HasIndex(item => new { item.Status, item.ReceivedUtc });
    }
}

public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OrderingDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=OrderingDb_DesignTime;Trusted_Connection=True")
        .Options);
}
