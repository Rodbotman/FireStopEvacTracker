using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEmailToEvacJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientEmail",
                table: "EvacJobs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientEmail",
                table: "EvacJobs");
        }
    }
}
