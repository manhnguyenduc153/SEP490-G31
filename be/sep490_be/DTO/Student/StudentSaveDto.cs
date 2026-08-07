using System;
using sep490_be.Helpers;

namespace sep490_be.DTO.Student
{
    public class StudentSaveDto
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
        public string? SchoolName { get; set; }
        public int? GradeLevel { get; set; }
        public string? ParentName { get; set; }
        public string? ParentPhone { get; set; }
        public string? Avatar { get; set; }
        public bool CreateAccount { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Email, Phone, ParentName, ParentPhone, SchoolName, Description);
    }
}

