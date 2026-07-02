using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseIdToLearningMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "learning_materials",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_materials_CourseId",
                table: "learning_materials",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_learning_materials_courses_CourseId",
                table: "learning_materials",
                column: "CourseId",
                principalTable: "courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_learning_materials_courses_CourseId",
                table: "learning_materials");

            migrationBuilder.DropIndex(
                name: "IX_learning_materials_CourseId",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "learning_materials");
        }
    }
}
