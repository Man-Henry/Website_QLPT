using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Models;

namespace Website_QLPT.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomImage> RoomImages { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<MaintenanceTicket> MaintenanceTickets { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Property -> IdentityUser (Owner)
            builder.Entity<Property>()
                .HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Room -> Property
            builder.Entity<Room>()
                .HasOne(r => r.Property)
                .WithMany(p => p.Rooms)
                .HasForeignKey(r => r.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // RoomImage -> Room: Cascade delete images when a room is deleted
            builder.Entity<RoomImage>()
                .HasOne(ri => ri.Room)
                .WithMany(r => r.Images)
                .HasForeignKey(ri => ri.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Contract -> Room
            builder.Entity<Contract>()
                .HasOne(c => c.Room)
                .WithMany(r => r.Contracts)
                .HasForeignKey(c => c.RoomId)
                .OnDelete(DeleteBehavior.Restrict); // Don't cascade-delete contracts when room deleted

            // Contract -> Tenant
            builder.Entity<Contract>()
                .HasOne(c => c.Tenant)
                .WithMany(t => t.Contracts)
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Tenant>()
                .HasOne(t => t.IdentityUser)
                .WithMany()
                .HasForeignKey(t => t.IdentityUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Tenant>()
                .HasIndex(t => t.IdentityUserId)
                .IsUnique()
                .HasFilter("[IdentityUserId] IS NOT NULL");

            // Invoice -> Contract
            builder.Entity<Invoice>()
                .HasOne(i => i.Contract)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Invoice>()
                .HasIndex(i => new { i.ContractId, i.Month, i.Year })
                .IsUnique();
        }
    }
}
