using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotAndDayColumnsToStudentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredDaysOfWeek",
                table: "student_registrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredSlotIndex",
                table: "student_registrations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredDaysOfWeek",
                table: "student_registrations");

            migrationBuilder.DropColumn(
                name: "PreferredSlotIndex",
                table: "student_registrations");
        }
    }
}
