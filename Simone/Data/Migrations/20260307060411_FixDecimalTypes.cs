using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuponesUsados_Promociones_PromocionID1",
                table: "CuponesUsados");

            migrationBuilder.DropIndex(
                name: "IX_CuponesUsados_PromocionID1",
                table: "CuponesUsados");

            migrationBuilder.DropColumn(
                name: "PromocionID1",
                table: "CuponesUsados");

            migrationBuilder.AlterColumn<decimal>(
                name: "PorcentajeComision",
                table: "Comisiones",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PromocionID1",
                table: "CuponesUsados",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PorcentajeComision",
                table: "Comisiones",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.CreateIndex(
                name: "IX_CuponesUsados_PromocionID1",
                table: "CuponesUsados",
                column: "PromocionID1");

            migrationBuilder.AddForeignKey(
                name: "FK_CuponesUsados_Promociones_PromocionID1",
                table: "CuponesUsados",
                column: "PromocionID1",
                principalTable: "Promociones",
                principalColumn: "PromocionID");
        }
    }
}
