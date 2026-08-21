using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToStudentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "student_registrations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "student_registrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "student_registrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "student_registrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "student_registrations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "student_registrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "student_registrations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "student_registrations");
        }
    }
}
