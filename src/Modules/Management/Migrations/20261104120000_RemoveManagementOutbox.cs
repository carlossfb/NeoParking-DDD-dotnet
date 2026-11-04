using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Management.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManagementOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nenhum serviço de Management grava nessa tabela. Se houver linhas, algo inesperado
            // aconteceu: a migration falha antes do DROP, e a decisão fica com quem opera o banco.
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_check_management_outbox_empty;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_check_management_outbox_empty()
                BEGIN
                    DECLARE v_rows INT;
                    DECLARE v_message VARCHAR(128);

                    SELECT COUNT(*) INTO v_rows FROM management_outbox_messages;
                    IF v_rows > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: management_outbox_messages has ', v_rows, ' row(s). Review them first.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;
                END;");
            migrationBuilder.Sql("CALL np_check_management_outbox_empty();");
            migrationBuilder.Sql("DROP PROCEDURE np_check_management_outbox_empty;");

            migrationBuilder.DropTable(
                name: "management_outbox_messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "management_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeadLetteredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_outbox_messages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_management_outbox_published_at",
                table: "management_outbox_messages",
                column: "PublishedAt");
        }
    }
}
