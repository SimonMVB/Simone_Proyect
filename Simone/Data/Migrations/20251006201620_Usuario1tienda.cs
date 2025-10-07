using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    /// <inheritdoc />
    public partial class Usuario1tienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TiendaId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TiendaId",
                table: "AspNetUsers",
                column: "TiendaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_TiendaId",
                table: "AspNetUsers",
                column: "TiendaId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_TiendaId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TiendaId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TiendaId",
                table: "AspNetUsers");
        }
    }
}
