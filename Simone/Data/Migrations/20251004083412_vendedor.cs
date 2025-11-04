using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Data.Migrations
{
    public partial class vendedor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Ajuste de longitud del nombre (como ya tenías)
            migrationBuilder.AlterColumn<string>(
                name: "NombreSubcategoria",
                table: "Subcategorias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // 2) Agregar VendedorID como NULL primero (sin default)
            migrationBuilder.AddColumn<string>(
                name: "VendedorID",
                table: "Subcategorias",
                type: "nvarchar(450)",
                nullable: true);

            // 3) Backfill: asigna un usuario válido a filas existentes
            migrationBuilder.Sql(@"
DECLARE @OwnerId nvarchar(450) =
(
    SELECT TOP 1 Id
    FROM AspNetUsers
    WHERE UserName IN ('admin','admin@simone.com','admin@yourdomain.com')
    ORDER BY Id
);
IF @OwnerId IS NULL
BEGIN
    SELECT TOP 1 @OwnerId = Id FROM AspNetUsers ORDER BY Id;
END
IF @OwnerId IS NULL
    THROW 50001, 'No hay usuarios en AspNetUsers para asignar como dueño de Subcategorias.', 1;

UPDATE S
   SET VendedorID = @OwnerId
  FROM Subcategorias S
 WHERE S.VendedorID IS NULL;
");

            // 4) Verificación de duplicados antes del índice único
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM (
        SELECT VendedorID, CategoriaID, NombreSubcategoria, COUNT(*) AS Cnt
        FROM Subcategorias
        GROUP BY VendedorID, CategoriaID, NombreSubcategoria
    ) t
    WHERE t.Cnt > 1
)
    THROW 50002, 'Existen subcategorías duplicadas por (VendedorID, CategoriaID, NombreSubcategoria). Limpia antes de aplicar el índice único.', 1;
");

            // 5) Volver NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "VendedorID",
                table: "Subcategorias",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            // 6) Índice único de negocio
            migrationBuilder.CreateIndex(
                name: "IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria",
                table: "Subcategorias",
                columns: new[] { "VendedorID", "CategoriaID", "NombreSubcategoria" },
                unique: true);

            // 7) FK (ya no habrá huérfanas)
            migrationBuilder.AddForeignKey(
                name: "FK_Subcategorias_AspNetUsers_VendedorID",
                table: "Subcategorias",
                column: "VendedorID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subcategorias_AspNetUsers_VendedorID",
                table: "Subcategorias");

            migrationBuilder.DropIndex(
                name: "IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria",
                table: "Subcategorias");

            migrationBuilder.DropColumn(
                name: "VendedorID",
                table: "Subcategorias");

            migrationBuilder.AlterColumn<string>(
                name: "NombreSubcategoria",
                table: "Subcategorias",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
