using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Billing.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "billing_outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "billing_outbox_messages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "billing_outbox_messages",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "billing_outbox_messages",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "billing_outbox_messages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "billing_outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "billing_outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "billing_outbox_messages");
        }
    }
}
