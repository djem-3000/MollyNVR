using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncCameraProfilesWithDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE camera_profiles
                SET AudioCodec = 'Unknown'
                WHERE AudioEnabled = 1 AND AudioCodec IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AudioEnabled",
                table: "camera_profiles");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "camera_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AudioEnabled",
                table: "camera_profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "camera_profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE camera_profiles
                SET AudioEnabled = CASE WHEN AudioCodec IS NULL THEN 0 ELSE 1 END;

                UPDATE camera_profiles
                SET Kind = CASE
                    WHEN Id = (
                        SELECT cameras.MainProfileId
                        FROM cameras
                        WHERE cameras.Id = camera_profiles.CameraId)
                    THEN 0
                    ELSE 1
                END;
                """);
        }
    }
}
