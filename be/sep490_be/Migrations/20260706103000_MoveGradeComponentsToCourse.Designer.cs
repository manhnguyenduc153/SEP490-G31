using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sep490_be.Models;

#nullable disable

namespace sep490_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706103000_MoveGradeComponentsToCourse")]
    partial class MoveGradeComponentsToCourse
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.12");
#pragma warning restore 612, 618
        }
    }
}
