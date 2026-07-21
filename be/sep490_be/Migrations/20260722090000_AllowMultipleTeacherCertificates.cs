using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sep490_be.Models;

#nullable disable

namespace sep490_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260722090000_AllowMultipleTeacherCertificates")]
    public partial class AllowMultipleTeacherCertificates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Certificate",
                table: "teachers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [teachers]
                SET [Certificate] = CONCAT('["', STRING_ESCAPE([Certificate], 'json'), '"]')
                WHERE [Certificate] IS NOT NULL
                  AND LTRIM(RTRIM([Certificate])) <> ''
                  AND ISJSON([Certificate]) = 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [teachers]
                SET [Certificate] = JSON_VALUE([Certificate], '$[0]')
                WHERE [Certificate] IS NOT NULL
                  AND ISJSON([Certificate]) = 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Certificate",
                table: "teachers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
