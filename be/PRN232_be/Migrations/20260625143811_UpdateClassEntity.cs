using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClassEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScheduleDisplay",
                table: "classes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeacherId",
                table: "classes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_classes_TeacherId",
                table: "classes",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_teachers_TeacherId",
                table: "classes",
                column: "TeacherId",
                principalTable: "teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_teachers_TeacherId",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_TeacherId",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "ScheduleDisplay",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "classes");
        }
    }
}
