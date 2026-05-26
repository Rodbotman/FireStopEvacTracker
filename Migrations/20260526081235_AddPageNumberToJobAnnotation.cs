using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPageNumberToJobAnnotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobAnnotation_JobApprovalId",
                table: "JobAnnotation");

            migrationBuilder.AddColumn<int>(
                name: "PageNumber",
                table: "JobAnnotation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_JobAnnotation_JobApprovalId_PageNumber",
                table: "JobAnnotation",
                columns: new[] { "JobApprovalId", "PageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobAnnotation_JobApprovalId_PageNumber",
                table: "JobAnnotation");

            migrationBuilder.DropColumn(
                name: "PageNumber",
                table: "JobAnnotation");

            migrationBuilder.CreateIndex(
                name: "IX_JobAnnotation_JobApprovalId",
                table: "JobAnnotation",
                column: "JobApprovalId",
                unique: true);
        }
    }
}
