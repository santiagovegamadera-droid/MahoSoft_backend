using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahoSoft.Datos.Migrations
{
    /// <inheritdoc />
    public partial class RecuperarPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResetTokenExpira",
                table: "Usuarios",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResetTokenHash",
                table: "Usuarios",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ResetTokenHash",
                table: "Usuarios",
                column: "ResetTokenHash",
                filter: "[ResetTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ResetTokenHash",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ResetTokenExpira",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ResetTokenHash",
                table: "Usuarios");
        }
    }
}
