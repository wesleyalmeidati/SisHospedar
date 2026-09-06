using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisHospedar.Migrations
{
    /// <inheritdoc />
    public partial class SyncHospedeLoginEReservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Hospedes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Hospedes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenhaHash",
                table: "Hospedes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SolicitouEncerramento",
                table: "Hospedagens",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    ReservaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospedeId = table.Column<int>(type: "int", nullable: false),
                    ApartamentoId = table.Column<int>(type: "int", nullable: false),
                    DataEntrada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataSaidaPrevista = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.ReservaId);
                    table.ForeignKey(
                        name: "FK_Reservas_Apartamentos_ApartamentoId",
                        column: x => x.ApartamentoId,
                        principalTable: "Apartamentos",
                        principalColumn: "ApartamentoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservas_Hospedes_HospedeId",
                        column: x => x.HospedeId,
                        principalTable: "Hospedes",
                        principalColumn: "HospedeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hospedes_Login",
                table: "Hospedes",
                column: "Login",
                unique: true,
                filter: "[Login] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_ApartamentoId",
                table: "Reservas",
                column: "ApartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_HospedeId",
                table: "Reservas",
                column: "HospedeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Hospedes_Login",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "SenhaHash",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "SolicitouEncerramento",
                table: "Hospedagens");
        }
    }
}
