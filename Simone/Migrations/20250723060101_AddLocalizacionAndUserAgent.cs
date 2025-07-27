using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simone.Migrations
{
    public partial class AddLocalizacionAndUserAgent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Asegura valores válidos antes de aplicar NOT NULL
            migrationBuilder.Sql(
                "UPDATE [LogIniciosSesion] SET [FechaInicio] = '2000-01-01T00:00:00.000' WHERE [FechaInicio] IS NULL;"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaInicio",
                table: "LogIniciosSesion",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DireccionIP",
                table: "LogIniciosSesion",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "Localizacion",
                table: "LogIniciosSesion",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "LogIniciosSesion",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Localizacion",
                table: "LogIniciosSesion");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "LogIniciosSesion");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaInicio",
                table: "LogIniciosSesion",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "DireccionIP",
                table: "LogIniciosSesion",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(45)",
                oldMaxLength: 45);
        }
    }
}
