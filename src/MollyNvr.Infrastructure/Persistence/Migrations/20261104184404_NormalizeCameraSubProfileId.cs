using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
        /// <summary>Fills the selected substream with the main profile when it was not set.</summary>
    public partial class NormalizeCameraSubProfileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE cameras SET SubProfileId = MainProfileId WHERE SubProfileId IS NULL OR SubProfileId = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE cameras SET SubProfileId = NULL WHERE SubProfileId = MainProfileId;");
        }
    }
}
