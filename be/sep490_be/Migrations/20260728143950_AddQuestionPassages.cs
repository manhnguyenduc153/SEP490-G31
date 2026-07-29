using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionPassages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassageId",
                table: "questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassageId",
                table: "exam_questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionPassageId",
                table: "exam_questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "question_passages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AudioUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkillType = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextSearch = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_passages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_passages_question_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "question_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questions_PassageId",
                table: "questions",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_questions_QuestionPassageId",
                table: "exam_questions",
                column: "QuestionPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_question_passages_CategoryId",
                table: "question_passages",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_questions_question_passages_QuestionPassageId",
                table: "exam_questions",
                column: "QuestionPassageId",
                principalTable: "question_passages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_questions_question_passages_PassageId",
                table: "questions",
                column: "PassageId",
                principalTable: "question_passages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_questions_question_passages_QuestionPassageId",
                table: "exam_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_questions_question_passages_PassageId",
                table: "questions");

            migrationBuilder.DropTable(
                name: "question_passages");

            migrationBuilder.DropIndex(
                name: "IX_questions_PassageId",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "IX_exam_questions_QuestionPassageId",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "PassageId",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "PassageId",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "QuestionPassageId",
                table: "exam_questions");
        }
    }
}
