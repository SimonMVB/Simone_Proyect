using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIconoClassAAtributos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Icono",
                table: "CategoriaAtributos",
                newName: "IconoClass");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IconoClass",
                table: "CategoriaAtributos",
                newName: "Icono");
        }
    }
}
