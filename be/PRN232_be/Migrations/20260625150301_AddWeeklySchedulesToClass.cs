using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklySchedulesToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoRefund",
                table: "classes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedLessons",
                table: "classes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeeklySchedulesJson",
                table: "classes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoRefund",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "ExpectedLessons",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "WeeklySchedulesJson",
                table: "classes");
        }
    }
}
