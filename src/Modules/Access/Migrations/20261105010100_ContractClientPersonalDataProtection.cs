using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <summary>
    /// Última fase da proteção do nome e do telefone: tira o nome das mensagens já gravadas no outbox e remove
    /// o texto puro, depois do preenchimento feito por AccessDatabaseMigrator. Se alguma linha não estiver
    /// protegida, falha antes de alterar qualquer coisa. Cada passo verifica o estado atual, porque o MySQL não
    /// tem DDL transacional e uma nova tentativa precisa concluir uma execução interrompida.
    /// </summary>
    public partial class ContractClientPersonalDataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_contract_client_personal_data;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_contract_client_personal_data()
                BEGIN
                    DECLARE v_unprotected INT;
                    DECLARE v_message VARCHAR(128);

                    SELECT COUNT(*) INTO v_unprotected FROM clients
                     WHERE name_encrypted IS NULL OR phone_encrypted IS NULL;
                    IF v_unprotected > 0 THEN
                        SET v_message = CONCAT('Cannot migrate: ', v_unprotected, ' clients are not protected. Start the API to fill them.');
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_message;
                    END IF;

                    ALTER TABLE clients
                        MODIFY COLUMN name_encrypted varchar(1200) CHARACTER SET utf8mb4 NOT NULL,
                        MODIFY COLUMN phone_encrypted varchar(128) CHARACTER SET utf8mb4 NOT NULL;

                    UPDATE outbox_messages
                       SET Payload = JSON_REMOVE(Payload, '$.Name')
                     WHERE `Type` = 'NeoParking.Shared.Kernel.Events.ClientRegisteredIntegrationEvent'
                       AND JSON_VALID(Payload)
                       AND JSON_CONTAINS_PATH(Payload, 'one', '$.Name');

                    IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
                                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients'
                                  AND COLUMN_NAME = 'phone_number') THEN
                        ALTER TABLE clients DROP COLUMN Name, DROP COLUMN phone_number;
                    END IF;
                END;");
            migrationBuilder.Sql("CALL np_contract_client_personal_data();");
            migrationBuilder.Sql("DROP PROCEDURE np_contract_client_personal_data;");
        }

        // O texto puro não volta sem a chave, e o SQL não a conhece. Com clientes, recusa em vez de perder
        // os dados deles: para voltar atrás, restaure um backup anterior a esta migration.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS np_revert_client_personal_data;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE np_revert_client_personal_data()
                BEGIN
                    IF EXISTS (SELECT 1 FROM clients) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot revert: client data is encrypted. Restore a backup instead.';
                    END IF;

                    ALTER TABLE clients
                        ADD COLUMN Name varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                        ADD COLUMN phone_number varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                        MODIFY COLUMN name_encrypted varchar(1200) CHARACTER SET utf8mb4 NULL,
                        MODIFY COLUMN phone_encrypted varchar(128) CHARACTER SET utf8mb4 NULL;
                END;");
            migrationBuilder.Sql("CALL np_revert_client_personal_data();");
            migrationBuilder.Sql("DROP PROCEDURE np_revert_client_personal_data;");
        }
    }
}
