using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class PermitirFechaAgregadoNulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()",
                defaultValue: DateTime.UtcNow,
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETDATE()");
        }
    }
}
