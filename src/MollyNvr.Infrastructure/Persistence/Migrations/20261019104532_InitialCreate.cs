using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    RtspPort = table.Column<int>(type: "INTEGER", nullable: false),
                    HttpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    OnvifPort = table.Column<int>(type: "INTEGER", nullable: false),
                    VendorKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Credentials = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cameras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "camera_profiles",
                columns: table => new
                {
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    FrameRate = table.Column<double>(type: "REAL", nullable: false),
                    BitrateKbps = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    AudioSampleRate = table.Column<int>(type: "INTEGER", nullable: true),
                    RtspUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camera_profiles", x => new { x.CameraId, x.Id });
                    table.ForeignKey(
                        name: "FK_camera_profiles_cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "camera_profiles");

            migrationBuilder.DropTable(
                name: "cameras");
        }
    }
}
