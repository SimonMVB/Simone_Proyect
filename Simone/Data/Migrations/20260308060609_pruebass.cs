using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class pruebass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerPath",
                table: "Vendedores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Vendedores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Vendedores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Vendedores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Vendedores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TikTokUrl",
                table: "Vendedores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Verificado",
                table: "Vendedores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReservasStock",
                columns: table => new
                {
                    ReservaStockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoID = table.Column<int>(type: "int", nullable: false),
                    ProductoVarianteID = table.Column<int>(type: "int", nullable: true),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SesionPosId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Confirmada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasStock", x => x.ReservaStockId);
                    table.ForeignKey(
                        name: "FK_ReservasStock_ProductoVariantes_ProductoVarianteID",
                        column: x => x.ProductoVarianteID,
                        principalTable: "ProductoVariantes",
                        principalColumn: "ProductoVarianteID");
                    table.ForeignKey(
                        name: "FK_ReservasStock_Productos_ProductoID",
                        column: x => x.ProductoID,
                        principalTable: "Productos",
                        principalColumn: "ProductoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasStock_ProductoID",
                table: "ReservasStock",
                column: "ProductoID");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasStock_ProductoVarianteID",
                table: "ReservasStock",
                column: "ProductoVarianteID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservasStock");

            migrationBuilder.DropColumn(
                name: "BannerPath",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "TikTokUrl",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "Verificado",
                table: "Vendedores");
        }
    }
}
