using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class version10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PrecioUnitario",
                table: "CarritoDetalle",
                newName: "Precio");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETDATE()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Precio",
                table: "CarritoDetalle",
                newName: "PrecioUnitario");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
