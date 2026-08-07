using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Semester : StandardEntity<int>
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; } // 1: Active/Registering, 2: Ongoing, 3: Completed
        public string? OriginalScheduleDraftJson { get; set; }

        public virtual ICollection<TeacherAvailability> TeacherAvailabilities { get; set; } = new List<TeacherAvailability>();
        public virtual ICollection<StudentRegistration> StudentRegistrations { get; set; } = new List<StudentRegistration>();
        public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    }
}

