using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL não tem DDL transacional: a verificação vem antes de qualquer índice, para a
            // migration falhar sem alterar nada. Dados duplicados nunca são apagados aqui.
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_check_access_unique_constraints;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_check_access_unique_constraints()
                BEGIN
                    DECLARE v_duplicates INT;
                    DECLARE v_message VARCHAR(128);

                    SELECT COUNT(*) INTO v_duplicates FROM (SELECT plate FROM vehicles GROUP BY plate HAVING COUNT(*) > 1) AS d;
                    IF v_duplicates > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: duplicate plates in vehicles (', v_duplicates, '). Fix using scripts/mysql/detect-duplicates.sql.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;

                    SELECT COUNT(*) INTO v_duplicates FROM (SELECT cpf FROM clients GROUP BY cpf HAVING COUNT(*) > 1) AS d;
                    IF v_duplicates > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: duplicate CPFs in clients (', v_duplicates, '). Fix using scripts/mysql/detect-duplicates.sql.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;

                    SELECT COUNT(*) INTO v_duplicates FROM (SELECT plate FROM tickets WHERE Status = 'Open' GROUP BY plate HAVING COUNT(*) > 1) AS d;
                    IF v_duplicates > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: plates with several open tickets (', v_duplicates, '). Fix using scripts/mysql/detect-duplicates.sql.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;
                END;");
            migrationBuilder.Sql("CALL np_check_access_unique_constraints();");
            migrationBuilder.Sql("DROP PROCEDURE np_check_access_unique_constraints;");
            migrationBuilder.CreateIndex(
                name: "ux_vehicles_plate",
                table: "vehicles",
                column: "plate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_clients_cpf",
                table: "clients",
                column: "cpf",
                unique: true);
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_tickets_open_plate ON tickets ((CASE WHEN Status = 'Open' THEN plate END));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ux_tickets_open_plate ON tickets;");
            migrationBuilder.DropIndex(
                name: "ux_vehicles_plate",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "ux_clients_cpf",
                table: "clients");
        }
    }
}
