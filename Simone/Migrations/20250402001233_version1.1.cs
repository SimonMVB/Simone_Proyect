using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class version11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaID",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Usuario",
                table: "LogIniciosSesion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaInicio",
                table: "LogIniciosSesion",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaID",
                table: "Productos",
                column: "CategoriaID");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_CategoriaID",
                table: "Productos",
                column: "CategoriaID",
                principalTable: "Categorias",
                principalColumn: "CategoriaID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_CategoriaID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaID",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaID",
                table: "Productos");

            migrationBuilder.AlterColumn<string>(
                name: "Usuario",
                table: "LogIniciosSesion",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaInicio",
                table: "LogIniciosSesion",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);
        }
    }
}
