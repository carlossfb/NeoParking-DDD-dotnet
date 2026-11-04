using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Management.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL não tem DDL transacional: a verificação vem antes de qualquer índice, para a
            // migration falhar sem alterar nada. Dados duplicados nunca são apagados aqui.
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_check_management_unique_constraints;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_check_management_unique_constraints()
                BEGIN
                    DECLARE v_duplicates INT;
                    DECLARE v_message VARCHAR(128);

                    SELECT COUNT(*) INTO v_duplicates FROM (SELECT 1 FROM management_operators WHERE Role = 'Owner' HAVING COUNT(*) > 1) AS d;
                    IF v_duplicates > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: more than one Owner (', v_duplicates, '). Fix using scripts/mysql/detect-duplicates.sql.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;
                END;");
            migrationBuilder.Sql("CALL np_check_management_unique_constraints();");
            migrationBuilder.Sql("DROP PROCEDURE np_check_management_unique_constraints;");

            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_management_operators_owner ON management_operators ((CASE WHEN Role = 'Owner' THEN 1 END));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ux_management_operators_owner ON management_operators;");

        }
    }
}
