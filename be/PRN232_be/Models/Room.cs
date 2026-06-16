using System.Collections.Generic;
using PRN232_be.Enums;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Room : StandardEntity<int>
    {
        public int? Capacity { get; set; }
        public int Status { get; set; }
        public RoomType RoomType { get; set; }

        public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}
