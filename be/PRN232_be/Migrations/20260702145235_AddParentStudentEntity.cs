using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddParentStudentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentName",
                table: "parent_students");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "parent_students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "parent_students",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "parent_students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "parent_students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "parent_students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "parent_students",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "parent_students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "parent_students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextSearch",
                table: "parent_students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "parent_students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "parent_students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "parent_students",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "TextSearch",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "parent_students");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "parent_students");

            migrationBuilder.AddColumn<string>(
                name: "ParentName",
                table: "parent_students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
