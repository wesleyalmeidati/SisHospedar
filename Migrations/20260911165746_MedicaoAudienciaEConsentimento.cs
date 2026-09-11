using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisHospedar.Migrations
{
    /// <inheritdoc />
    public partial class MedicaoAudienciaEConsentimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentimentosCookie",
                columns: table => new
                {
                    ConsentimentoCookieId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentificadorAnonimo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Aceito = table.Column<bool>(type: "bit", nullable: false),
                    VersaoPolitica = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentimentosCookie", x => x.ConsentimentoCookieId);
                });

            migrationBuilder.CreateTable(
                name: "VisitasPagina",
                columns: table => new
                {
                    VisitaPaginaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Caminho = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    TotalRecorrentes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitasPagina", x => x.VisitaPaginaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimentosCookie_IdentificadorAnonimo",
                table: "ConsentimentosCookie",
                column: "IdentificadorAnonimo");

            migrationBuilder.CreateIndex(
                name: "IX_VisitasPagina_Caminho_Data",
                table: "VisitasPagina",
                columns: new[] { "Caminho", "Data" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentimentosCookie");

            migrationBuilder.DropTable(
                name: "VisitasPagina");
        }
    }
}
