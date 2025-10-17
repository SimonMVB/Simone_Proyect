using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardeningProductoVarianteYGaleria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarritoDetalle_Carrito_CarritoID",
                table: "CarritoDetalle");

            migrationBuilder.DropForeignKey(
                name: "FK_Favoritos_AspNetUsers_UsuarioId",
                table: "Favoritos");

            migrationBuilder.DropTable(
                name: "AsistenciaEmpleados");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_VendedorID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_ProductoImagenes_ProductoID",
                table: "ProductoImagenes");

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "ProductoVariantes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Talla",
                table: "Productos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Productos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoVariantes_SKU",
                table: "ProductoVariantes",
                column: "SKU",
                unique: true,
                filter: "[SKU] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes",
                sql: "[PrecioVenta] > 0 AND [Stock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaID_SubcategoriaID",
                table: "Productos",
                columns: new[] { "CategoriaID", "SubcategoriaID" });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_VendedorID_Nombre",
                table: "Productos",
                columns: new[] { "VendedorID", "Nombre" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Producto_Precios",
                table: "Productos",
                sql: "[PrecioCompra] >= 0 AND [PrecioVenta] >= 0 AND [Stock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoImagenes_ProductoID_Path",
                table: "ProductoImagenes",
                columns: new[] { "ProductoID", "Path" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CarritoDetalle_Carrito_CarritoID",
                table: "CarritoDetalle",
                column: "CarritoID",
                principalTable: "Carrito",
                principalColumn: "CarritoID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Favoritos_AspNetUsers_UsuarioId",
                table: "Favoritos",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarritoDetalle_Carrito_CarritoID",
                table: "CarritoDetalle");

            migrationBuilder.DropForeignKey(
                name: "FK_Favoritos_AspNetUsers_UsuarioId",
                table: "Favoritos");

            migrationBuilder.DropIndex(
                name: "IX_ProductoVariantes_SKU",
                table: "ProductoVariantes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaID_SubcategoriaID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_VendedorID_Nombre",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Producto_Precios",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_ProductoImagenes_ProductoID_Path",
                table: "ProductoImagenes");

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "ProductoVariantes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Talla",
                table: "Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AsistenciaEmpleados",
                columns: table => new
                {
                    AsistenciaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoID = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoraEntrada = table.Column<TimeSpan>(type: "time", nullable: true),
                    HoraSalida = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistenciaEmpleados", x => x.AsistenciaID);
                    table.ForeignKey(
                        name: "FK_AsistenciaEmpleados_Empleados_EmpleadoID",
                        column: x => x.EmpleadoID,
                        principalTable: "Empleados",
                        principalColumn: "EmpleadoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaID",
                table: "Productos",
                column: "CategoriaID");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_VendedorID",
                table: "Productos",
                column: "VendedorID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoImagenes_ProductoID",
                table: "ProductoImagenes",
                column: "ProductoID");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciaEmpleados_EmpleadoID",
                table: "AsistenciaEmpleados",
                column: "EmpleadoID");

            migrationBuilder.AddForeignKey(
                name: "FK_CarritoDetalle_Carrito_CarritoID",
                table: "CarritoDetalle",
                column: "CarritoID",
                principalTable: "Carrito",
                principalColumn: "CarritoID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Favoritos_AspNetUsers_UsuarioId",
                table: "Favoritos",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
