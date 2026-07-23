using System;

namespace sep490_be.DTO.Teacher
{
    public class TeacherDto
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
        public string? Avatar { get; set; }
        public List<string> Certificates { get; set; } = new();
        public bool HasAccount { get; set; }
    }
}

