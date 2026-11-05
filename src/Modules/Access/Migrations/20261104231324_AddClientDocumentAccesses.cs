using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoParking.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDocumentAccesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_document_accesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    operator_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    accessed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_document_accesses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_client_document_accesses_client_id",
                table: "client_document_accesses",
                column: "client_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_document_accesses");
        }
    }
}
