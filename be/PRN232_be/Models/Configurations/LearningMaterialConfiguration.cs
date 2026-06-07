using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class LearningMaterialConfiguration : IEntityTypeConfiguration<LearningMaterial>
    {
        public void Configure(EntityTypeBuilder<LearningMaterial> builder)
        {
            builder.ToTable("learning_materials");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

            builder.Property(x => x.Title).HasMaxLength(250);
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.FileUrl).HasMaxLength(500);
            builder.Property(x => x.FileType).HasMaxLength(50);

            builder.HasOne(x => x.Class)
                .WithMany(c => c.LearningMaterials)
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ClassSchedule)
                .WithMany(cs => cs.LearningMaterials)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Teacher)
                .WithMany(t => t.LearningMaterials)
                .HasForeignKey(x => x.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
