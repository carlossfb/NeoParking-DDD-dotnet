namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Outbox;

public sealed class AccessDbContext : DbContext
{
    // 200 caracteres de até 4 bytes, mais nonce e tag, em base64 e com a versão da chave.
    public const int NameEncryptedMaxLength = 1200;
    public const int PhoneEncryptedMaxLength = 128;

    private readonly IDocumentProtector _protector;

    internal IDocumentProtector Protector => _protector;

    public DbSet<Client> Clients { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<ClientDocumentAccess> ClientDocumentAccesses { get; set; }

    public AccessDbContext(DbContextOptions<AccessDbContext> options, IDocumentProtector protector) : base(options) =>
        _protector = protector;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, ProtectorModelCacheKeyFactory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(builder =>
        {
            builder.ToTable("clients");
            builder.HasKey(c => c.Id);

            // A cifra fica no mapeamento: o domínio e o serviço continuam vendo o nome e o telefone.
            // Comparar ou ordenar por essas colunas não funciona, porque cada gravação gera um valor diferente.
            var protector = _protector;

            builder.Property(c => c.Name)
                .HasColumnName("name_encrypted")
                .HasMaxLength(NameEncryptedMaxLength)
                .IsRequired()
                .HasConversion(
                    name => protector.ProtectField(ClientField.Name, name),
                    value => protector.RevealField(ClientField.Name, value));

            builder.Property(c => c.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(c => c.DocumentEncrypted)
                .HasColumnName("document_encrypted")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(c => c.DocumentHmac)
                .HasColumnName("document_hmac")
                .HasMaxLength(64)
                .IsRequired();

            builder.Ignore(c => c.Document);

            builder.Property(c => c.PhoneNumber)
                .HasColumnName("phone_encrypted")
                .HasMaxLength(PhoneEncryptedMaxLength)
                .IsRequired()
                .HasConversion(
                    phone => protector.ProtectField(ClientField.PhoneNumber, phone.Value),
                    value => PhoneNumber.CreateBR(protector.RevealField(ClientField.PhoneNumber, value)))
                .Metadata.SetValueComparer(new ValueComparer<PhoneNumber>(
                    (a, b) => a!.Value == b!.Value,
                    c => c.Value.GetHashCode(),
                    c => PhoneNumber.CreateBR(c.Value)));

            builder.HasIndex(c => c.DocumentHmac)
                .IsUnique()
                .HasDatabaseName("ux_clients_document_hmac");

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

        // Sem chave estrangeira: o registro de quem leu um documento sobrevive à exclusão do cliente.
        modelBuilder.Entity<ClientDocumentAccess>(builder =>
        {
            builder.ToTable("client_document_accesses");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.ClientId).HasColumnName("client_id");
            builder.Property(a => a.OperatorId).HasColumnName("operator_id");
            builder.Property(a => a.AccessedAt).HasColumnName("accessed_at");
            builder.HasIndex(a => a.ClientId).HasDatabaseName("ix_client_document_accesses_client_id");
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
/// <summary>
/// O EF guarda o modelo por processo, e o modelo guarda o protetor nas conversões.
/// Sem separar por protetor, um contexto com outras chaves usaria as chaves do primeiro.
/// </summary>
internal sealed class ProtectorModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as AccessDbContext)?.Protector, designTime);
}
