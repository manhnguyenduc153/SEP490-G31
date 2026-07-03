using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Semester : StandardEntity<int>
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; } // 1: Active/Registering, 2: Ongoing, 3: Completed

        public virtual ICollection<TeacherAvailability> TeacherAvailabilities { get; set; } = new List<TeacherAvailability>();
        public virtual ICollection<StudentRegistration> StudentRegistrations { get; set; } = new List<StudentRegistration>();
    }
}
