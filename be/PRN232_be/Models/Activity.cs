using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Activity : BaseEntity<int>
    {
        public int? ClassId { get; set; }
        public int? ScheduleId { get; set; }
        public int Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; }
        public decimal? TotalScore { get; set; }
        public decimal? PassingScore { get; set; }
        public int? MaxAttempts { get; set; }
        public bool AllowLateSubmit { get; set; }
        public bool ShuffleQuestion { get; set; }
        public bool ShowAnswerAfter { get; set; }
        public int Status { get; set; }

        public virtual Class? Class { get; set; }
        public virtual ClassSchedule? ClassSchedule { get; set; }
        public virtual ICollection<ActivityQuestion> ActivityQuestions { get; set; } = new List<ActivityQuestion>();
        public virtual ICollection<ActivityAttempt> ActivityAttempts { get; set; } = new List<ActivityAttempt>();
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
        public virtual ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
    }
}
