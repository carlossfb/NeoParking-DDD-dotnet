using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeoParking.Access.Infrastructure;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <summary>
    /// Primeira fase da proteção do documento: só acrescenta as colunas. O preenchimento é feito em
    /// <see cref="AccessDatabaseMigrator"/>, e a remoção do texto puro em ContractClientDocumentProtection.
    /// Só usa SQL, por isso não tem modelo de destino.
    /// </summary>
    [DbContext(typeof(AccessDbContext))]
    [Migration("20261104231500_ExpandClientDocumentProtection")]
    public partial class ExpandClientDocumentProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    ADD COLUMN document_encrypted varchar(128) CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN document_hmac varchar(64) CHARACTER SET utf8mb4 NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    DROP COLUMN document_encrypted,
                    DROP COLUMN document_hmac;");
        }
    }
}
