using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEntitiesStandardExceptRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "questions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "questions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "questions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "questions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "questions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "questions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "question_categories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "question_categories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "question_categories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "question_categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "question_categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "question_categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "question_categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "question_categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "question_categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "question_categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "question_answers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "question_answers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "question_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "question_answers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "question_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "question_answers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "question_answers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "question_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "question_answers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "question_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "notifications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "learning_materials",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "learning_materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "learning_materials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "learning_materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "learning_materials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "learning_materials",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "learning_materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "learning_materials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "learning_materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "exam_schedules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "exam_schedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "exam_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "exam_schedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "exam_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "exam_schedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "exam_schedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "exam_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "exam_schedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "exam_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "class_schedules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "class_schedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "class_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "class_schedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "class_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "class_schedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "class_schedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "class_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "class_schedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "class_schedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "attendances",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "attendances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "attendances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "attendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "attendances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "attendances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "attendances",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "attendances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "attendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "attendances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "activity_attempts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "activity_attempts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "activity_attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "activity_attempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "activity_attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "activity_attempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "activity_attempts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "activity_attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "activity_attempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "activity_attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "activity_answers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "activity_answers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "activity_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "activity_answers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "activity_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "activity_answers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "activity_answers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "activity_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "activity_answers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "activity_answers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "activities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "activities",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "activities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "activities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "activities",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "question_categories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "learning_materials");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "class_schedules");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "activity_answers");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "activities");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "question_categories",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
