using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisHospedar.Migrations
{
    /// <inheritdoc />
    public partial class AddHospedeIdToFuncionario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HospedeId",
                table: "Funcionarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_HospedeId",
                table: "Funcionarios",
                column: "HospedeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Funcionarios_Hospedes_HospedeId",
                table: "Funcionarios",
                column: "HospedeId",
                principalTable: "Hospedes",
                principalColumn: "HospedeId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Funcionarios_Hospedes_HospedeId",
                table: "Funcionarios");

            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_HospedeId",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "HospedeId",
                table: "Funcionarios");
        }
    }
}
