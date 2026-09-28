using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Web.Data;

public sealed class StaffWebDbContext(DbContextOptions<StaffWebDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<StoredTicket> Tickets => Set<StoredTicket>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredTicket>(entity =>
        {
            entity.HasKey(ticket => ticket.Key);
            entity.Property(ticket => ticket.Key).HasMaxLength(64);
            entity.Property(ticket => ticket.ProtectedValue).IsRequired();
            entity.HasIndex(ticket => ticket.ExpiresUtc);
        });
    }
}

public sealed class StoredTicket
{
    public string Key { get; set; } = string.Empty;
    public byte[] ProtectedValue { get; set; } = [];
    public DateTimeOffset ExpiresUtc { get; set; }
}
