using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddClientType : Migration
    {
        // Um único ALTER: renomeia sem tocar nos valores e é atômico no InnoDB. O scaffold apagava a coluna.
        // Quem já existe só pode ser Individual, porque até aqui a coluna só aceitava CPF.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    CHANGE COLUMN cpf document varchar(14) CHARACTER SET utf8mb4 NOT NULL,
                    ADD COLUMN type varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Individual',
                    RENAME INDEX ux_clients_cpf TO ux_clients_document;");
        }

        // Com um CNPJ gravado, o ALTER falha por tamanho e nada muda: não há como representá-lo na coluna antiga.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    CHANGE COLUMN document cpf varchar(11) CHARACTER SET utf8mb4 NOT NULL,
                    DROP COLUMN type,
                    RENAME INDEX ux_clients_document TO ux_clients_cpf;");
        }
    }
}
