using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class modificacion_Producto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Alto",
                table: "Productos",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ancho",
                table: "Productos",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Largo",
                table: "Productos",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Peso",
                table: "Productos",
                type: "decimal(10,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alto",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Ancho",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Largo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Peso",
                table: "Productos");
        }
    }
}
