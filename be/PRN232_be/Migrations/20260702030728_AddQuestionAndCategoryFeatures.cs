using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAndCategoryFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Point column already exists in the questions table in the database from a previous manual run.
            // Safely drop the Tags column if it exists.
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.columns WHERE Name = 'Tags' AND Object_ID = Object_ID('questions')) ALTER TABLE questions DROP COLUMN Tags;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
