using Microsoft.EntityFrameworkCore;
using main.infrastructure.persistence.mysql.model;

namespace main.infrastructure
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientModel> Clients { get; set; }
        public DbSet<VehicleModel> Vehicles { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientModel>()
                .HasMany(c => c.Vehicles)
                .WithOne(v => v.Client)
                .HasForeignKey(v => v.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}