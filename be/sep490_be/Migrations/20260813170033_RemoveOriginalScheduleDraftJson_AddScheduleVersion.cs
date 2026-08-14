using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOriginalScheduleDraftJson_AddScheduleVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalScheduleDraftJson",
                table: "semesters");

            migrationBuilder.CreateTable(
                name: "schedule_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SemesterId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScheduleJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedule_versions_semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_versions_SemesterId",
                table: "schedule_versions",
                column: "SemesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_versions");

            migrationBuilder.AddColumn<string>(
                name: "OriginalScheduleDraftJson",
                table: "semesters",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
