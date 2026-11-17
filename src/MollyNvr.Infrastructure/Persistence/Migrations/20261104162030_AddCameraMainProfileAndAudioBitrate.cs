using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraMainProfileAndAudioBitrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SelectedSubProfileId",
                table: "cameras",
                newName: "SubProfileId");

            migrationBuilder.AddColumn<string>(
                name: "MainProfileId",
                table: "cameras",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AudioBitrateKbps",
                table: "camera_profiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE cameras
                SET MainProfileId = COALESCE(
                    (SELECT Id FROM camera_profiles
                     WHERE camera_profiles.CameraId = cameras.Id AND Kind = 0
                     LIMIT 1),
                    (SELECT Id FROM camera_profiles
                     WHERE camera_profiles.CameraId = cameras.Id
                     ORDER BY Width * Height DESC
                     LIMIT 1),
                    '');
                """);

            migrationBuilder.Sql(
                """
                UPDATE cameras
                SET VendorKey = 'onvif'
                WHERE lower(VendorKey) = 'reolink';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainProfileId",
                table: "cameras");

            migrationBuilder.DropColumn(
                name: "AudioBitrateKbps",
                table: "camera_profiles");

            migrationBuilder.RenameColumn(
                name: "SubProfileId",
                table: "cameras",
                newName: "SelectedSubProfileId");
        }
    }
}
