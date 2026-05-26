using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotImagePathToJobAnnotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotImagePath",
                table: "JobAnnotation",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotImagePath",
                table: "JobAnnotation");
        }
    }
}
