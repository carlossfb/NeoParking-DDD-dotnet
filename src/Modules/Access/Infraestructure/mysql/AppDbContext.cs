namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Domain;

public sealed class AccessDbContext : DbContext
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }

    public AccessDbContext(DbContextOptions<AccessDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(builder =>
        {
            builder.ToTable("clients");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).HasMaxLength(200).IsRequired();

            builder.Property(c => c.Cpf)
                .HasColumnName("cpf")
                .HasMaxLength(11)
                .IsRequired()
                .HasConversion(
                    cpf => cpf.Document,
                    value => Cpf.Create(value));

            builder.Property(c => c.PhoneNumber)
                .HasColumnName("phone_number")
                .HasMaxLength(20)
                .IsRequired()
                .HasConversion(
                    phone => phone.Value,
                    value => PhoneNumber.CreateBR(value));
            builder.HasMany(c => c.Vehicles)
                .WithOne()
                .HasForeignKey(v => v.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(c => c.Vehicles)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Vehicle>(builder =>
        {
            builder.ToTable("vehicles");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Plate)
                .HasColumnName("plate")
                .HasMaxLength(7)
                .IsRequired()
                .HasConversion(
                    plate => plate.Value,
                    value => Plate.Create(value));
        });
    }
}