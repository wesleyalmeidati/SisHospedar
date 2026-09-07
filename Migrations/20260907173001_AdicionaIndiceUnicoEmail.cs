using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisHospedar.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndiceUnicoEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Hospedes_Email",
                table: "Hospedes",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Email",
                table: "Funcionarios",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hospedes_Email",
                table: "Hospedes");

            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_Email",
                table: "Funcionarios");
        }
    }
}
