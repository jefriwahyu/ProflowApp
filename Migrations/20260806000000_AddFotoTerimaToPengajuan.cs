using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFlowApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFotoTerimaToPengajuan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoTerima",
                table: "Pengajuan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TglTerima",
                table: "Pengajuan",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoTerima",
                table: "Pengajuan");

            migrationBuilder.DropColumn(
                name: "TglTerima",
                table: "Pengajuan");
        }
    }
}
