using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    /// <inheritdoc />
    public partial class tiendamod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleVentas_Ventas_VentaID",
                table: "DetalleVentas");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_Productos_ProductoID",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID",
                table: "CarritoDetalle");

            migrationBuilder.AddColumn<int>(
                name: "ProductoVarianteID",
                table: "MovimientosInventario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductoVarianteID",
                table: "DetalleVentas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductoVarianteID",
                table: "CarritoDetalle",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductoVariantes",
                columns: table => new
                {
                    ProductoVarianteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoID = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Talla = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrecioCompra = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImagenPath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoVariantes", x => x.ProductoVarianteID);
                    table.ForeignKey(
                        name: "FK_ProductoVariantes_Productos_ProductoID",
                        column: x => x.ProductoID,
                        principalTable: "Productos",
                        principalColumn: "ProductoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_ProductoVarianteID",
                table: "MovimientosInventario",
                column: "ProductoVarianteID");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleVentas_ProductoVarianteID",
                table: "DetalleVentas",
                column: "ProductoVarianteID");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID_ProductoVarianteID",
                table: "CarritoDetalle",
                columns: new[] { "CarritoID", "ProductoID", "ProductoVarianteID" },
                unique: true,
                filter: "[ProductoVarianteID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_ProductoVarianteID",
                table: "CarritoDetalle",
                column: "ProductoVarianteID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoVariantes_ProductoID_Color_Talla",
                table: "ProductoVariantes",
                columns: new[] { "ProductoID", "Color", "Talla" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CarritoDetalle_ProductoVariantes_ProductoVarianteID",
                table: "CarritoDetalle",
                column: "ProductoVarianteID",
                principalTable: "ProductoVariantes",
                principalColumn: "ProductoVarianteID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleVentas_ProductoVariantes_ProductoVarianteID",
                table: "DetalleVentas",
                column: "ProductoVarianteID",
                principalTable: "ProductoVariantes",
                principalColumn: "ProductoVarianteID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleVentas_Ventas_VentaID",
                table: "DetalleVentas",
                column: "VentaID",
                principalTable: "Ventas",
                principalColumn: "VentaID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_ProductoVariantes_ProductoVarianteID",
                table: "MovimientosInventario",
                column: "ProductoVarianteID",
                principalTable: "ProductoVariantes",
                principalColumn: "ProductoVarianteID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Productos_ProductoID",
                table: "MovimientosInventario",
                column: "ProductoID",
                principalTable: "Productos",
                principalColumn: "ProductoID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarritoDetalle_ProductoVariantes_ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.DropForeignKey(
                name: "FK_DetalleVentas_ProductoVariantes_ProductoVarianteID",
                table: "DetalleVentas");

            migrationBuilder.DropForeignKey(
                name: "FK_DetalleVentas_Ventas_VentaID",
                table: "DetalleVentas");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_ProductoVariantes_ProductoVarianteID",
                table: "MovimientosInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_Productos_ProductoID",
                table: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "ProductoVariantes");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_ProductoVarianteID",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_DetalleVentas_ProductoVarianteID",
                table: "DetalleVentas");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID_ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.DropColumn(
                name: "ProductoVarianteID",
                table: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "ProductoVarianteID",
                table: "DetalleVentas");

            migrationBuilder.DropColumn(
                name: "ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID",
                table: "CarritoDetalle",
                columns: new[] { "CarritoID", "ProductoID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleVentas_Ventas_VentaID",
                table: "DetalleVentas",
                column: "VentaID",
                principalTable: "Ventas",
                principalColumn: "VentaID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Productos_ProductoID",
                table: "MovimientosInventario",
                column: "ProductoID",
                principalTable: "Productos",
                principalColumn: "ProductoID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
