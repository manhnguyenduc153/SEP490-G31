using System;

namespace PRN232_be.DTO.Semester
{
    public class SemesterDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
    }
}
