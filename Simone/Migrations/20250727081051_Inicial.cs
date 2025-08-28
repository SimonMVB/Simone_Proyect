using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarritoDetalle_Productos_ProductoID1",
                table: "CarritoDetalle");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_ProductoID1",
                table: "CarritoDetalle");

            migrationBuilder.DropColumn(
                name: "ProductoID1",
                table: "CarritoDetalle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductoID1",
                table: "CarritoDetalle",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_ProductoID1",
                table: "CarritoDetalle",
                column: "ProductoID1");

            migrationBuilder.AddForeignKey(
                name: "FK_CarritoDetalle_Productos_ProductoID1",
                table: "CarritoDetalle",
                column: "ProductoID1",
                principalTable: "Productos",
                principalColumn: "ProductoID");
        }
    }
}
