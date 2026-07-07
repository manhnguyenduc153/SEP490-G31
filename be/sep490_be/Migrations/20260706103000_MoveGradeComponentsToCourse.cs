using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232_be.Migrations
{
    public partial class MoveGradeComponentsToCourse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('grade_components', 'CourseId') IS NULL
BEGIN
    ALTER TABLE grade_components ADD CourseId int NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('grade_components', 'ClassId') IS NOT NULL
BEGIN
    EXEC('
        UPDATE gc
        SET CourseId = c.CourseId
        FROM grade_components gc
        INNER JOIN classes c ON c.Id = gc.ClassId
        WHERE gc.CourseId IS NULL AND c.CourseId IS NOT NULL;
    ');
END
");

            migrationBuilder.Sql(@"
DELETE FROM grade_components
WHERE CourseId IS NULL;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('FK_grade_components_classes_ClassId', 'F') IS NOT NULL
BEGIN
    ALTER TABLE grade_components DROP CONSTRAINT FK_grade_components_classes_ClassId;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_grade_components_ClassId_Code'
      AND object_id = OBJECT_ID('grade_components')
)
BEGIN
    DROP INDEX IX_grade_components_ClassId_Code ON grade_components;
END
");

            migrationBuilder.Sql(@"
;WITH component_map AS (
    SELECT
        Id,
        MIN(Id) OVER (PARTITION BY CourseId, Code) AS KeepId
    FROM grade_components
)
DELETE sgo
FROM student_grade_overrides sgo
INNER JOIN component_map cm ON cm.Id = sgo.GradeComponentId
WHERE cm.Id <> cm.KeepId
  AND EXISTS (
      SELECT 1
      FROM student_grade_overrides existing
      WHERE existing.StudentClassId = sgo.StudentClassId
        AND existing.GradeComponentId = cm.KeepId
  );
");

            migrationBuilder.Sql(@"
;WITH component_map AS (
    SELECT
        Id,
        MIN(Id) OVER (PARTITION BY CourseId, Code) AS KeepId
    FROM grade_components
)
UPDATE sgo
SET GradeComponentId = cm.KeepId
FROM student_grade_overrides sgo
INNER JOIN component_map cm ON cm.Id = sgo.GradeComponentId
WHERE cm.Id <> cm.KeepId;
");

            migrationBuilder.Sql(@"
;WITH component_map AS (
    SELECT
        Id,
        MIN(Id) OVER (PARTITION BY CourseId, Code) AS KeepId
    FROM grade_components
)
DELETE gc
FROM grade_components gc
INNER JOIN component_map cm ON cm.Id = gc.Id
WHERE cm.Id <> cm.KeepId;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('grade_components', 'ClassId') IS NOT NULL
BEGIN
    ALTER TABLE grade_components DROP COLUMN ClassId;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('grade_components')
      AND name = 'CourseId'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE grade_components ALTER COLUMN CourseId int NOT NULL;
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('FK_grade_components_courses_CourseId', 'F') IS NULL
BEGIN
    ALTER TABLE grade_components
    ADD CONSTRAINT FK_grade_components_courses_CourseId
    FOREIGN KEY (CourseId) REFERENCES courses(Id) ON DELETE CASCADE;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_grade_components_CourseId_Code'
      AND object_id = OBJECT_ID('grade_components')
)
BEGIN
    CREATE UNIQUE INDEX IX_grade_components_CourseId_Code
    ON grade_components(CourseId, Code);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('grade_components', 'ClassId') IS NULL
BEGIN
    ALTER TABLE grade_components ADD ClassId int NULL;
END

IF OBJECT_ID('FK_grade_components_courses_CourseId', 'F') IS NOT NULL
BEGIN
    ALTER TABLE grade_components DROP CONSTRAINT FK_grade_components_courses_CourseId;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_grade_components_CourseId_Code'
      AND object_id = OBJECT_ID('grade_components')
)
BEGIN
    DROP INDEX IX_grade_components_CourseId_Code ON grade_components;
END
");
        }
    }
}
