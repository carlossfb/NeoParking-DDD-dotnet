using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeoParking.Access.Infrastructure;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <summary>
    /// Primeira fase da proteção do nome e do telefone: só acrescenta as colunas. O preenchimento é feito em
    /// <see cref="AccessDatabaseMigrator"/>, e a remoção do texto puro em ContractClientPersonalDataProtection.
    /// Só usa SQL, por isso não tem modelo de destino.
    /// </summary>
    [DbContext(typeof(AccessDbContext))]
    [Migration("20261105010000_ExpandClientPersonalDataProtection")]
    public partial class ExpandClientPersonalDataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    ADD COLUMN name_encrypted varchar(1200) CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN phone_encrypted varchar(128) CHARACTER SET utf8mb4 NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE clients
                    DROP COLUMN name_encrypted,
                    DROP COLUMN phone_encrypted;");
        }
    }
}
