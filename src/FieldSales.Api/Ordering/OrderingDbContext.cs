using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldSales.Api.Ordering;

/// <summary>Work received from tablets and the orders it starts (T-4.1.2). The ledger holds every received item once,
/// exactly as sent; a received order also enters the order lifecycle as Pending, and a received Call keeps a row with its
/// stock entries for the next Morning Snapshot (T-5.4.1).</summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    private const string SubjectCollation = "Latin1_General_100_BIN2";

    public DbSet<ReceivedWork> ReceivedWork => Set<ReceivedWork>();
    public DbSet<ReceivedOrder> Orders => Set<ReceivedOrder>();
    public DbSet<ReceivedCall> Calls => Set<ReceivedCall>();
    public DbSet<ReceivedCallStockEntry> CallStockEntries => Set<ReceivedCallStockEntry>();
    public DbSet<ReceivedCallCompetitorNote> CallCompetitorNotes => Set<ReceivedCallCompetitorNote>();

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
        // The Suggested List reads a Location's newest Accepted orders (T-5.4.1).
        orders.HasIndex(item => new { item.LocationId, item.Status, item.TakenOn });

        var calls = modelBuilder.Entity<ReceivedCall>();
        calls.ToTable("ReceivedCalls", table => table.HasCheckConstraint("CK_ReceivedCalls_Received", """
            [LocationId] <> '00000000-0000-0000-0000-000000000000' AND LEN([CapturingRep]) > 0
            AND DATEPART(TZOFFSET, [ReceivedUtc]) = 0
            """.ReplaceLineEndings("\n")));
        calls.HasKey(item => item.Id);
        calls.Property(item => item.Id).ValueGeneratedNever();
        calls.Property(item => item.CapturingRep).HasMaxLength(450).UseCollation(SubjectCollation);
        calls.Property(item => item.Channel).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        calls.HasOne<ReceivedWork>().WithOne().HasForeignKey<ReceivedCall>(item => item.Id).OnDelete(DeleteBehavior.Restrict);
        calls.HasMany(item => item.StockEntries).WithOne().HasForeignKey(entry => entry.CallId).OnDelete(DeleteBehavior.Cascade);
        calls.HasMany(item => item.CompetitorNotes).WithOne().HasForeignKey(note => note.CallId).OnDelete(DeleteBehavior.Cascade);
        // A Location's last Stock Check is its newest In-person Call with stock entries (T-5.4.1).
        calls.HasIndex(item => new { item.LocationId, item.Channel, item.SavedAt });

        var entries = modelBuilder.Entity<ReceivedCallStockEntry>();
        entries.ToTable("ReceivedCallStockEntries", table => table.HasCheckConstraint("CK_ReceivedCallStockEntries_Entry",
            "[ProductId] <> '00000000-0000-0000-0000-000000000000' AND LEN([Unit]) > 0 AND ([Count] IS NULL OR [Count] >= 0)"));
        entries.HasKey(entry => new { entry.CallId, entry.ProductId });
        entries.Property(entry => entry.Unit).HasMaxLength(20).IsUnicode(false);
        entries.Property(entry => entry.Count).HasPrecision(18, 6);

        // T-5.8.1: one link type per note; a Range or Category note names exactly that, None and Products name neither.
        var notes = modelBuilder.Entity<ReceivedCallCompetitorNote>();
        notes.ToTable("ReceivedCallCompetitorNotes", table => table.HasCheckConstraint("CK_ReceivedCallCompetitorNotes_Link", """
            LEN([Text]) > 0 AND (
                ([LinkTo] = 'Range' AND [RangeId] IS NOT NULL AND [CategoryId] IS NULL)
                OR ([LinkTo] = 'Category' AND [CategoryId] IS NOT NULL AND [RangeId] IS NULL)
                OR ([LinkTo] IN ('None', 'Products') AND [RangeId] IS NULL AND [CategoryId] IS NULL))
            """.ReplaceLineEndings("\n")));
        notes.HasKey(note => new { note.CallId, note.Id });
        notes.Property(note => note.Text).HasMaxLength(FieldSales.Calls.Contracts.CallCompetitorNote.MaximumTextLength);
        notes.Property(note => note.LinkTo).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        notes.HasMany(note => note.Products).WithOne().HasForeignKey(product => new { product.CallId, product.NoteId })
            .OnDelete(DeleteBehavior.Cascade);

        var linked = modelBuilder.Entity<ReceivedCallCompetitorNoteProduct>();
        linked.ToTable("ReceivedCallCompetitorNoteProducts", table => table.HasCheckConstraint(
            "CK_ReceivedCallCompetitorNoteProducts_Product", "[ProductId] <> '00000000-0000-0000-0000-000000000000'"));
        linked.HasKey(product => new { product.CallId, product.NoteId, product.ProductId });
    }
}

public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OrderingDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=OrderingDb_DesignTime;Trusted_Connection=True")
        .Options);
}
