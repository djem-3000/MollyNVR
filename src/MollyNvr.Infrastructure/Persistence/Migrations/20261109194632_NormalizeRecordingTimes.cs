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
            // Время записи теперь хранится в UTC. Старые значения SQLite хранил
            // с локальным смещением, поэтому приводим их к UTC средствами SQLite.
            migrationBuilder.Sql(
                "UPDATE recording_segments SET StartedAt = datetime(StartedAt), EndedAt = datetime(EndedAt);");
            migrationBuilder.Sql(
                "UPDATE recording_events SET StartedAt = datetime(StartedAt), EndedAt = datetime(EndedAt) " +
                "WHERE EndedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратное преобразование к локальному смещению неоднозначно
            // без знания часового пояса, поэтому данные остаются в UTC.
        }
    }
}
