using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recording_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recording_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_segments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recording_settings",
                columns: table => new
                {
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContinuousEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContinuousSchedule = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    EventEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventSchedule = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    EventTypes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PreRollSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    PostRollSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_settings", x => x.CameraId);
                    table.ForeignKey(
                        name: "FK_recording_settings_cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    QuotaPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    HighWatermarkPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    LowWatermarkPercent = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recording_events_CameraId_StartedAt",
                table: "recording_events",
                columns: new[] { "CameraId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_recording_segments_CameraId",
                table: "recording_segments",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_recording_segments_StartedAt",
                table: "recording_segments",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recording_events");

            migrationBuilder.DropTable(
                name: "recording_segments");

            migrationBuilder.DropTable(
                name: "recording_settings");

            migrationBuilder.DropTable(
                name: "storage_settings");
        }
    }
}
