using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareCode",
                table: "EvacJobs",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ClientName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LayoutAccuracyApproved = table.Column<bool>(type: "INTEGER", nullable: true),
                    FireEquipmentLocationsApproved = table.Column<bool>(type: "INTEGER", nullable: true),
                    YouAreHereApproved = table.Column<bool>(type: "INTEGER", nullable: true),
                    DiagramMountingLocationApproved = table.Column<bool>(type: "INTEGER", nullable: true),
                    ChangesRequired = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApprovals_EvacJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "EvacJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobApprovals_JobId",
                table: "JobApprovals",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobApprovals");

            migrationBuilder.DropColumn(
                name: "ShareCode",
                table: "EvacJobs");
        }
    }
}
