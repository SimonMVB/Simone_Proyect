using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    /// <inheritdoc />
    public partial class Harde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes");

            migrationBuilder.DropIndex(
                name: "IX_ImagenesProductos_ProductoID",
                table: "ImagenesProductos");

            migrationBuilder.RenameColumn(
                name: "CreadoUtc",
                table: "ProductoImagenes",
                newName: "FechaSubidaUtc");

            migrationBuilder.AlterColumn<string>(
                name: "ImagenPath",
                table: "ProductoVariantes",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
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

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

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

            migrationBuilder.AlterColumn<bool>(
                name: "Principal",
                table: "ProductoImagenes",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "ProductoImagenes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Alto",
                table: "ProductoImagenes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ancho",
                table: "ProductoImagenes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ProductoImagenes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PesoBytes",
                table: "ProductoImagenes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RutaImagen",
                table: "ImagenesProductos",
                type: "varchar(300)",
                unicode: false,
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "ImagenesProductos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Principal",
                table: "ImagenesProductos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_ProductoVariante_Producto_Color_Talla",
                table: "ProductoVariantes",
                columns: new[] { "ProductoID", "Color", "Talla" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes",
                sql: "([PrecioCompra] IS NULL OR [PrecioCompra] >= 0) AND ([PrecioVenta] IS NULL OR [PrecioVenta] > 0) AND [Stock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImagenesProductos_Producto_Ruta",
                table: "ImagenesProductos",
                columns: new[] { "ProductoID", "RutaImagen" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProductoVariante_Producto_Color_Talla",
                table: "ProductoVariantes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes");

            migrationBuilder.DropIndex(
                name: "IX_ImagenesProductos_Producto_Ruta",
                table: "ImagenesProductos");

            migrationBuilder.DropColumn(
                name: "Alto",
                table: "ProductoImagenes");

            migrationBuilder.DropColumn(
                name: "Ancho",
                table: "ProductoImagenes");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ProductoImagenes");

            migrationBuilder.DropColumn(
                name: "PesoBytes",
                table: "ProductoImagenes");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "ImagenesProductos");

            migrationBuilder.DropColumn(
                name: "Principal",
                table: "ImagenesProductos");

            migrationBuilder.RenameColumn(
                name: "FechaSubidaUtc",
                table: "ProductoImagenes",
                newName: "CreadoUtc");

            migrationBuilder.AlterColumn<string>(
                name: "ImagenPath",
                table: "ProductoVariantes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
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

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAgregado",
                table: "Productos",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

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

            migrationBuilder.AlterColumn<bool>(
                name: "Principal",
                table: "ProductoImagenes",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "ProductoImagenes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "RutaImagen",
                table: "ImagenesProductos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(300)",
                oldUnicode: false,
                oldMaxLength: 300);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductoVariante_Valores",
                table: "ProductoVariantes",
                sql: "[PrecioVenta] > 0 AND [Stock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImagenesProductos_ProductoID",
                table: "ImagenesProductos",
                column: "ProductoID");
        }
    }
}
