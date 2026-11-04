namespace NeoParking.Management.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;

public sealed class ManagementDbContext : DbContext
{
    public DbSet<OperatorUser> Operators { get; set; }

    public ManagementDbContext(DbContextOptions<ManagementDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperatorUser>(builder =>
        {
            builder.ToTable("management_operators");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(u => u.Email)
                .HasMaxLength(320)
                .IsRequired();

            builder.HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("ix_management_operators_email");

            builder.Property(u => u.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(100)
                .IsRequired()
                .HasConversion(
                    ph => ph.Value,
                    value => PasswordHash.FromHash(value))
                .Metadata.SetValueComparer(new ValueComparer<PasswordHash>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => PasswordHash.FromHash(c.Value)));

            // Um único Owner: índice funcional ux_management_operators_owner, criado na
            // migration AddUniqueConstraints. O EF não representa esse tipo de índice no modelo.
            builder.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(u => u.IsActive).IsRequired();
            builder.Property(u => u.CreatedAt).IsRequired();
        });
    }
}
