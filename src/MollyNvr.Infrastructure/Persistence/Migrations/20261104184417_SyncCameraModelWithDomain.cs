using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
        /// <summary>Removes the camera port, renames the firmware column and makes the substream mandatory.</summary>
    public partial class SyncCameraModelWithDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HttpPort",
                table: "cameras");

            migrationBuilder.DropColumn(
                name: "OnvifPort",
                table: "cameras");

            migrationBuilder.RenameColumn(
                name: "FirmwareVersion",
                table: "cameras",
                newName: "Firmware");

            migrationBuilder.AlterColumn<string>(
                name: "SubProfileId",
                table: "cameras",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Firmware",
                table: "cameras",
                newName: "FirmwareVersion");

            migrationBuilder.AlterColumn<string>(
                name: "SubProfileId",
                table: "cameras",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<int>(
                name: "HttpPort",
                table: "cameras",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "OnvifPort",
                table: "cameras",
                type: "INTEGER",
                nullable: false,
                defaultValue: 8000);
        }
    }
}
