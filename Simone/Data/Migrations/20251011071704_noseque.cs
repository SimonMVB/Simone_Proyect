using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    /// <inheritdoc />
    public partial class noseque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_AspNetUsers_VendedorID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Favoritos_UsuarioId_ProductoId",
                table: "Favoritos");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID_ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.AlterColumn<string>(
                name: "Talla",
                table: "Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImagenPath",
                table: "Productos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Productos",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Favoritos_UsuarioId",
                table: "Favoritos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID",
                table: "CarritoDetalle",
                columns: new[] { "CarritoID", "ProductoID" },
                unique: true,
                filter: "[ProductoVarianteID] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoVarianteID",
                table: "CarritoDetalle",
                columns: new[] { "CarritoID", "ProductoVarianteID" },
                unique: true,
                filter: "[ProductoVarianteID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_AspNetUsers_VendedorID",
                table: "Productos",
                column: "VendedorID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_AspNetUsers_VendedorID",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Favoritos_UsuarioId",
                table: "Favoritos");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID",
                table: "CarritoDetalle");

            migrationBuilder.DropIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoVarianteID",
                table: "CarritoDetalle");

            migrationBuilder.AlterColumn<string>(
                name: "Talla",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImagenPath",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Favoritos_UsuarioId_ProductoId",
                table: "Favoritos",
                columns: new[] { "UsuarioId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarritoDetalle_CarritoID_ProductoID_ProductoVarianteID",
                table: "CarritoDetalle",
                columns: new[] { "CarritoID", "ProductoID", "ProductoVarianteID" },
                unique: true,
                filter: "[ProductoVarianteID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_AspNetUsers_VendedorID",
                table: "Productos",
                column: "VendedorID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
