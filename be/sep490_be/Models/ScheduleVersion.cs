using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class ScheduleVersion : AuditableEntity<int>
    {
        public int SemesterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ScheduleJson { get; set; } = string.Empty;
        public bool IsAutoSaved { get; set; }

        public virtual Semester Semester { get; set; } = null!;
    }
}
