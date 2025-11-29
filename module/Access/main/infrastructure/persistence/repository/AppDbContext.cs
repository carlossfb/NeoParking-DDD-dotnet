using Microsoft.EntityFrameworkCore;
using main.infrastructure.dao;

namespace main.infrastructure
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientDAO> Clients { get; set; }
        public DbSet<VehicleDAO> Vehicles { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientDAO>()
                .HasMany(c => c.Vehicles)
                .WithOne()
                .HasForeignKey(v => v.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}