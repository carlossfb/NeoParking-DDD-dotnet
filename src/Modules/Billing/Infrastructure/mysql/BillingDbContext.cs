namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Outbox;

public sealed class BillingDbContext : DbContext
{
    public DbSet<PricingPolicy> PricingPolicies { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Comparer null-safe: reaproveitado tanto para colunas obrigatórias (PricingPolicy)
        // quanto para as colunas opcionais de Fee em Ticket (nulas enquanto Open).
        var moneyComparer = new ValueComparer<Money>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.Value == b.Value),
            m => m == null ? 0 : m.Value.GetHashCode(),
            m => m == null ? null! : Money.Create(m.Value));

        modelBuilder.Entity<PricingPolicy>(builder =>
        {
            builder.ToTable("billing_pricing_policies");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.OwnerId).IsRequired();

            builder.Property(p => p.MinimumFee)
                .HasColumnName("minimum_fee")
                .HasColumnType("decimal(10,2)")
                .IsRequired()
                .HasConversion(m => m.Value, value => Money.Create(value))
                .Metadata.SetValueComparer(moneyComparer);

            builder.Property(p => p.HourlyRate)
                .HasColumnName("hourly_rate")
                .HasColumnType("decimal(10,2)")
                .IsRequired()
                .HasConversion(m => m.Value, value => Money.Create(value))
                .Metadata.SetValueComparer(moneyComparer);

            builder.Property(p => p.MinimumFeeCoverageMinutes)
                .HasColumnName("minimum_fee_coverage_minutes")
                .IsRequired();

            builder.Property(p => p.GracePeriodMinutes)
                .HasColumnName("grace_period_minutes")
                .IsRequired();

            builder.Property(p => p.EffectiveFrom).IsRequired();
            builder.Property(p => p.EffectiveTo);

            // Consulta mais comum: "qual política está ativa" / "qual estava vigente em X".
            builder.HasIndex(p => p.EffectiveTo)
                .HasDatabaseName("ix_billing_pricing_policies_effective_to");

            builder.HasIndex(p => new { p.EffectiveFrom, p.EffectiveTo })
                .HasDatabaseName("ix_billing_pricing_policies_effective_range");

            // Uma política ativa: índice funcional ux_billing_pricing_policies_active, criado na
            // migration AddUniqueConstraints. O EF não representa esse tipo de índice no modelo.
        });

        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.ToTable("billing_tickets");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Plate)
                .HasMaxLength(10)
                .IsRequired();

            builder.HasIndex(t => t.Plate)
                .HasDatabaseName("ix_billing_tickets_plate");

            builder.Property(t => t.EntryAt).IsRequired();
            builder.Property(t => t.ExitAt);

            builder.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            // Campos "congelados" da Fee — nulos enquanto o ticket está Open.
            builder.Property(t => t.FeePricingPolicyId).HasColumnName("fee_pricing_policy_id");

            builder.Property(t => t.FeeMinimumFee)
                .HasColumnName("fee_minimum_fee")
                .HasColumnType("decimal(10,2)")
                .HasConversion(
                    m => m == null ? (decimal?)null : m.Value,
                    value => value == null ? null : Money.Create(value.Value))
                .Metadata.SetValueComparer(moneyComparer);

            builder.Property(t => t.FeeHourlyRate)
                .HasColumnName("fee_hourly_rate")
                .HasColumnType("decimal(10,2)")
                .HasConversion(
                    m => m == null ? (decimal?)null : m.Value,
                    value => value == null ? null : Money.Create(value.Value))
                .Metadata.SetValueComparer(moneyComparer);

            builder.Property(t => t.FeeBilledHours).HasColumnName("fee_billed_hours");

            builder.Property(t => t.FeeAmount)
                .HasColumnName("fee_amount")
                .HasColumnType("decimal(10,2)")
                .HasConversion(
                    m => m == null ? (decimal?)null : m.Value,
                    value => value == null ? null : Money.Create(value.Value))
                .Metadata.SetValueComparer(moneyComparer);

            builder.Property(t => t.FeeCalculatedAt).HasColumnName("fee_calculated_at");

            // ChargedFee é computada em memória a partir dos campos Fee* — não é uma coluna.
            builder.Ignore(t => t.ChargedFee);

            // Consulta mais comum: histórico de cobranças por data de fechamento.
            builder.HasIndex(t => t.FeeCalculatedAt)
                .HasDatabaseName("ix_billing_tickets_fee_calculated_at");
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("billing_outbox_messages");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Type).HasMaxLength(500).IsRequired();
            builder.Property(o => o.Payload).HasColumnType("longtext").IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.PublishedAt).IsRequired(false);
            builder.Property(o => o.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

            builder.HasIndex(o => o.PublishedAt)
                .HasDatabaseName("ix_billing_outbox_published_at");
        });
    }
}
