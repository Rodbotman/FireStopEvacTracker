using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireStopEvacTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBilledToEvacJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBilled",
                table: "EvacJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "JobDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EvacJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DocumentPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobDocuments_EvacJobs_EvacJobId",
                        column: x => x.EvacJobId,
                        principalTable: "EvacJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobDocuments_EvacJobId",
                table: "JobDocuments",
                column: "EvacJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobDocuments");

            migrationBuilder.DropColumn(
                name: "IsBilled",
                table: "EvacJobs");
        }
    }
}
