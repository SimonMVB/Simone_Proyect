using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class CompleteUserProfileSecurityAndActivitySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DireccionIP",
                table: "LogIniciosSesion",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "LogIniciosSesion",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActividadesUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detalles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActividadesUsuarios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogIniciosSesion_UsuarioId",
                table: "LogIniciosSesion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesUsuarios_UsuarioId",
                table: "ActividadesUsuarios",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogIniciosSesion_AspNetUsers_UsuarioId",
                table: "LogIniciosSesion",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogIniciosSesion_AspNetUsers_UsuarioId",
                table: "LogIniciosSesion");

            migrationBuilder.DropTable(
                name: "ActividadesUsuarios");

            migrationBuilder.DropIndex(
                name: "IX_LogIniciosSesion_UsuarioId",
                table: "LogIniciosSesion");

            migrationBuilder.DropColumn(
                name: "DireccionIP",
                table: "LogIniciosSesion");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "LogIniciosSesion");
        }
    }
}
