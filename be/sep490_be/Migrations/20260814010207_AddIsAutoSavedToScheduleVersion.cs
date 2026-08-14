using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sep490_be.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAutoSavedToScheduleVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutoSaved",
                table: "schedule_versions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [schedule_versions] SET [IsAutoSaved] = 1 WHERE [Name] = N'Bản gốc (tự động)';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutoSaved",
                table: "schedule_versions");
        }
    }
}
