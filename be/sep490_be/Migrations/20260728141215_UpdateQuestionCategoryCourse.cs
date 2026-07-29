using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class UpdateQuestionCategoryCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillType",
                table: "question_categories");

            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "question_categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_categories_CourseId",
                table: "question_categories",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_question_categories_courses_CourseId",
                table: "question_categories",
                column: "CourseId",
                principalTable: "courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_question_categories_courses_CourseId",
                table: "question_categories");

            migrationBuilder.DropIndex(
                name: "IX_question_categories_CourseId",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "question_categories");

            migrationBuilder.AddColumn<int>(
                name: "SkillType",
                table: "question_categories",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }
    }
}
