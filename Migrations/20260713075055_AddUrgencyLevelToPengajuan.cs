using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFlowApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUrgencyLevelToPengajuan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Entity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Barang",
                columns: table => new
                {
                    Brg_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nm_Brg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Satuan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hrg_Est = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Nm_Vendor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Barang", x => x.Brg_ID);
                });

            migrationBuilder.CreateTable(
                name: "Pengajuan",
                columns: table => new
                {
                    PR_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoPR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brg_ID = table.Column<int>(type: "int", nullable: false),
                    Jml = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Tgl_Req = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Keterangan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FotoBukti = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KetChecker = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TglChecker = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TglFeedback = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pengajuan", x => x.PR_ID);
                });

            migrationBuilder.CreateTable(
                name: "Pesanan",
                columns: table => new
                {
                    PO_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoPO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PR_ID = table.Column<int>(type: "int", nullable: false),
                    tgl_PO = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    TotalHarga = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pesanan", x => x.PO_ID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nama = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Barang");

            migrationBuilder.DropTable(
                name: "Pengajuan");

            migrationBuilder.DropTable(
                name: "Pesanan");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
