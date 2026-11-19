using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MollyNvr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeRecordingTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
                        // Recording times are now stored in UTC. SQLite kept the old values
                        // with a local offset, so they are converted to UTC using SQLite means.
            migrationBuilder.Sql(
                "UPDATE recording_segments SET StartedAt = datetime(StartedAt), EndedAt = datetime(EndedAt);");
            migrationBuilder.Sql(
                "UPDATE recording_events SET StartedAt = datetime(StartedAt), EndedAt = datetime(EndedAt) " +
                "WHERE EndedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
                        // Converting back to a local offset is ambiguous
                        // without knowing the time zone, so the data stays in UTC.
        }
    }
}
