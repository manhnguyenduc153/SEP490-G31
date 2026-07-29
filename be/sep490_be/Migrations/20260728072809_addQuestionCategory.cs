using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class addQuestionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "questions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillType",
                table: "questions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SkillType",
                table: "question_categories",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "SkillType",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "SkillType",
                table: "question_categories");
        }
    }
}
