using System.Collections.Generic;
using sep490_be.Enums;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Course : StandardEntity<int>
    {
        public int Status { get; set; }
        public int? Duration { get; set; }
        public double? Price { get; set; }
        public string? Description { get; set; }
        public GradeLevel? RequiredGradeLevel { get; set; }

        public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
        public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();
        public virtual ICollection<GradeComponent> GradeComponents { get; set; } = new List<GradeComponent>();
    }
}

