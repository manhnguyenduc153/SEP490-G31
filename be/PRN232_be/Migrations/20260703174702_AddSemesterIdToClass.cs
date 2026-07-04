using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterIdToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SemesterId",
                table: "classes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_classes_SemesterId",
                table: "classes",
                column: "SemesterId");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_semesters_SemesterId",
                table: "classes",
                column: "SemesterId",
                principalTable: "semesters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_semesters_SemesterId",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_SemesterId",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "SemesterId",
                table: "classes");
        }
    }
}
