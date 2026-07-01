using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingFloorToRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Building",
                table: "rooms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "rooms",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomImg",
                table: "rooms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomType",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Building",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "RoomImg",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "RoomType",
                table: "rooms");
        }
    }
}
