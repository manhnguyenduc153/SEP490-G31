using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class update_ielts_homework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AttachmentUrl",
                table: "HomeworkSubmissions",
                newName: "AttachmentUrls");

            migrationBuilder.RenameColumn(
                name: "AttachmentUrl",
                table: "Homeworks",
                newName: "Skill");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrls",
                table: "Homeworks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentUrls",
                table: "Homeworks");

            migrationBuilder.RenameColumn(
                name: "AttachmentUrls",
                table: "HomeworkSubmissions",
                newName: "AttachmentUrl");

            migrationBuilder.RenameColumn(
                name: "Skill",
                table: "Homeworks",
                newName: "AttachmentUrl");
        }
    }
}
