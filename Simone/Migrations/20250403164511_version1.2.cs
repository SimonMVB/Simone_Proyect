using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class version12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NombreProducto",
                table: "Productos",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "NombreCategoria",
                table: "Categorias",
                newName: "Nombre");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Ventas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Pedidos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaPedido",
                table: "Pedidos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Ventas");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Productos",
                newName: "NombreProducto");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Categorias",
                newName: "NombreCategoria");

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Pedidos",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaPedido",
                table: "Pedidos",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
