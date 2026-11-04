using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Todos recebem código: a coluna é obrigatória, e um ticket já encerrado encontrado
            // pelo código responde "já encerrado" em vez de 404. Só a nova coluna é alterada.
            // O formato é o de TicketCode.Generate: 10 bytes de fonte criptográfica em hexadecimal.
            migrationBuilder.Sql("UPDATE tickets SET code = HEX(RANDOM_BYTES(10));");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "exit_method",
                table: "tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "exit_operator_id",
                table: "tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "exit_reason",
                table: "tickets",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_tickets_code",
                table: "tickets",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_tickets_code",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "code",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "exit_method",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "exit_operator_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "exit_reason",
                table: "tickets");
        }
    }
}
