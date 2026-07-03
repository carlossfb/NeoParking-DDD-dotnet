using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Billing.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "billing_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_outbox_messages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "billing_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Plate = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntryAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExitAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fee_pricing_policy_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    fee_minimum_fee = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    fee_hourly_rate = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    fee_billed_hours = table.Column<int>(type: "int", nullable: true),
                    fee_amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    fee_calculated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_tickets", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "billing_pricing_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    minimum_fee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    minimum_fee_coverage_minutes = table.Column<int>(type: "int", nullable: false),
                    grace_period_minutes = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_pricing_policies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_billing_outbox_published_at",
                table: "billing_outbox_messages",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_billing_tickets_fee_calculated_at",
                table: "billing_tickets",
                column: "fee_calculated_at");

            migrationBuilder.CreateIndex(
                name: "ix_billing_tickets_plate",
                table: "billing_tickets",
                column: "Plate");

            migrationBuilder.CreateIndex(
                name: "ix_billing_pricing_policies_effective_range",
                table: "billing_pricing_policies",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "ix_billing_pricing_policies_effective_to",
                table: "billing_pricing_policies",
                column: "EffectiveTo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_outbox_messages");

            migrationBuilder.DropTable(
                name: "billing_tickets");

            migrationBuilder.DropTable(
                name: "billing_pricing_policies");
        }
    }
}
