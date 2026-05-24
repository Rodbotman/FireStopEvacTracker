using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAnnotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobAnnotation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobApprovalId = table.Column<int>(type: "INTEGER", nullable: false),
                    CanvasDataUrl = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAnnotation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobAnnotation_JobApprovals_JobApprovalId",
                        column: x => x.JobApprovalId,
                        principalTable: "JobApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobAnnotation_JobApprovalId",
                table: "JobAnnotation",
                column: "JobApprovalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobAnnotation");
        }
    }
}
