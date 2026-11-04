using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <summary>
    /// Última fase da proteção do documento: remove o texto puro, depois do preenchimento feito por
    /// AccessDatabaseMigrator. Se alguma linha não estiver protegida, falha antes de alterar qualquer coisa.
    /// Cada passo verifica o estado atual, porque o MySQL não tem DDL transacional e uma nova tentativa
    /// precisa concluir uma execução interrompida.
    /// </summary>
    public partial class ContractClientDocumentProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_contract_client_document;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_contract_client_document()
                BEGIN
                    DECLARE v_unprotected INT;
                    DECLARE v_message VARCHAR(128);

                    SELECT COUNT(*) INTO v_unprotected FROM clients
                     WHERE document_encrypted IS NULL OR document_hmac IS NULL;
                    IF v_unprotected > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: ', v_unprotected, ' client documents are not protected. Start the API to fill them.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;

                    ALTER TABLE clients
                        MODIFY COLUMN document_encrypted varchar(128) CHARACTER SET utf8mb4 NOT NULL,
                        MODIFY COLUMN document_hmac varchar(64) CHARACTER SET utf8mb4 NOT NULL;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients'
                                      AND INDEX_NAME = 'ux_clients_document_hmac') THEN
                        CREATE UNIQUE INDEX ux_clients_document_hmac ON clients (document_hmac);
                    END IF;

                    IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
                                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients'
                                  AND COLUMN_NAME = 'document') THEN
                        ALTER TABLE clients DROP COLUMN document;
                    END IF;
                END;");
            migrationBuilder.Sql("CALL np_contract_client_document();");
            migrationBuilder.Sql("DROP PROCEDURE np_contract_client_document;");
        }

        // O texto puro não volta sem a chave, e o SQL não a conhece. Com clientes, recusa em vez de perder
        // o documento deles: para voltar atrás, restaure um backup anterior a esta migration.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_revert_client_document;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_revert_client_document()
                BEGIN
                    IF EXISTS (SELECT 1 FROM clients) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot revert: client documents are encrypted. Restore a backup instead.';
                    END IF;

                    ALTER TABLE clients
                        ADD COLUMN document varchar(14) CHARACTER SET utf8mb4 NOT NULL,
                        ADD UNIQUE INDEX ux_clients_document (document),
                        MODIFY COLUMN document_encrypted varchar(128) CHARACTER SET utf8mb4 NULL,
                        MODIFY COLUMN document_hmac varchar(64) CHARACTER SET utf8mb4 NULL,
                        DROP INDEX ux_clients_document_hmac;
                END;");
            migrationBuilder.Sql("CALL np_revert_client_document();");
            migrationBuilder.Sql("DROP PROCEDURE np_revert_client_document;");
        }
    }
}
