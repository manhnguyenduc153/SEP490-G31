using System;

namespace sep490_be.DTO.Class
{
    public class ClassScheduleDto
    {
        public int Id { get; set; }
        public int? ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public int? LessonNo { get; set; }
        public DateTime? ScheduleDate { get; set; }
        public int? SlotId { get; set; }
        public string? SlotName { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherAvatar { get; set; }
        public int Status { get; set; }
        public string? Note { get; set; }
        public int? AttendanceStatus { get; set; }
    }
}


