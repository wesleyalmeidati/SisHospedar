using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisHospedar.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCamposIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Hospedes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "Hospedes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "Hospedes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Hospedes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Funcionarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "Funcionarios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "Funcionarios",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Funcionarios",
                type: "nvarchar(max)",
                nullable: true);

            // PT: Linhas criadas antes desta migration (fora do UserManager) ficam com
            //     SecurityStamp nulo — preenche com um valor único pra cada uma, já que
            //     o token de redefinição de senha depende dele pra invalidar depois de
            //     usado. Contas novas ganham o próprio stamp automaticamente via
            //     UserManager.CreateAsync.
            // EN: Rows created before this migration (outside UserManager) end up with
            //     a null SecurityStamp — backfills a unique value for each, since the
            //     password-reset token depends on it to invalidate after use. New
            //     accounts get their own stamp automatically via UserManager.CreateAsync.
            migrationBuilder.Sql("UPDATE Hospedes SET SecurityStamp = CONVERT(nvarchar(36), NEWID()) WHERE SecurityStamp IS NULL;");
            migrationBuilder.Sql("UPDATE Funcionarios SET SecurityStamp = CONVERT(nvarchar(36), NEWID()) WHERE SecurityStamp IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Hospedes");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Funcionarios");
        }
    }
}
