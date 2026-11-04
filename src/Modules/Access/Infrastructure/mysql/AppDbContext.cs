namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Outbox;

public sealed class AccessDbContext : DbContext
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

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
                    value => Cpf.Create(value))
                .Metadata.SetValueComparer(new ValueComparer<Cpf>(
                    (a, b) => a!.Document == b!.Document,
                    c => c.Document.GetHashCode(),
                    c => Cpf.Create(c.Document)));

            builder.Property(c => c.PhoneNumber)
                .HasColumnName("phone_number")
                .HasMaxLength(20)
                .IsRequired()
                .HasConversion(
                    phone => phone.Value,
                    value => PhoneNumber.CreateBR(value))
                .Metadata.SetValueComparer(new ValueComparer<PhoneNumber>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => PhoneNumber.CreateBR(c.Value)));

            builder.HasIndex(c => c.Cpf)
                .IsUnique()
                .HasDatabaseName("ux_clients_cpf");

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
                    value => Plate.Create(value))
                .Metadata.SetValueComparer(new ValueComparer<Plate>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => Plate.Create(c.Value)));

            builder.HasIndex(v => v.Plate)
                .IsUnique()
                .HasDatabaseName("ux_vehicles_plate");
        });

        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.ToTable("tickets");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Plate)
                .HasColumnName("plate")
                .HasMaxLength(7)
                .IsRequired()
                .HasConversion(
                    plate => plate.Value,
                    value => Plate.Create(value))
                .Metadata.SetValueComparer(new ValueComparer<Plate>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => Plate.Create(c.Value)));

            builder.Property(t => t.Code)
                .HasColumnName("code")
                .HasMaxLength(TicketCode.Length)
                .IsRequired()
                .HasConversion(
                    code => code.Value,
                    value => TicketCode.Create(value))
                .Metadata.SetValueComparer(new ValueComparer<TicketCode>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => TicketCode.Create(c.Value)));

            builder.HasIndex(t => t.Code)
                .IsUnique()
                .HasDatabaseName("ux_tickets_code");

            builder.Property(t => t.ClientId).HasColumnName("client_id");
            builder.Property(t => t.EntryAt).HasColumnName("entry_at").IsRequired();
            builder.Property(t => t.ExitAt).HasColumnName("exit_at");

            builder.Property(t => t.ExitMethod)
                .HasColumnName("exit_method")
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(t => t.ExitReason)
                .HasColumnName("exit_reason")
                .HasMaxLength(Ticket.ExitReasonMaxLength);

            builder.Property(t => t.ExitOperatorId).HasColumnName("exit_operator_id");

            // A saída só grava se o ticket continuar aberto: duas saídas simultâneas não publicam dois eventos.
            builder.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired()
                .IsConcurrencyToken();

            builder.HasIndex(t => t.Plate)
                .HasDatabaseName("ix_tickets_plate");

            builder.HasIndex(t => t.Status)
                .HasDatabaseName("ix_tickets_status");

            // Um ticket aberto por placa: índice funcional ux_tickets_open_plate, criado na
            // migration AddUniqueConstraints. O EF não representa esse tipo de índice no modelo.
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Type)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(o => o.Payload)
                .HasColumnType("longtext")
                .IsRequired();

            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.PublishedAt).IsRequired(false);
            builder.Property(o => o.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

            // índice para o worker buscar mensagens pendentes eficientemente
            builder.HasIndex(o => o.PublishedAt)
                .HasDatabaseName("ix_outbox_messages_published_at");
        });
    }
}
