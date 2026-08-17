using System;
using sep490_be.Enums;
using sep490_be.Helpers;

namespace sep490_be.DTO.Teacher
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
        public int Status { get; set; } = 1;
        public string? Description { get; set; }
        public int? GradeLevel { get; set; }
        public string? Avatar { get; set; }
        public List<string> Certificates { get; set; } = new();

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Email, Phone);
    }
}

