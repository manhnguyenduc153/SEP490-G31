using System;
using PRN232_be.Helpers;
using PRN232_be.Enums;

namespace PRN232_be.DTO.Teacher
{
    public class TeacherSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? Dob { get; set; }
        public bool? Gender { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }
        public GradeLevel? GradeLevel { get; set; }
        public string? Avatar { get; set; }
        public string? Certificate { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Email, Phone);
    }
}
