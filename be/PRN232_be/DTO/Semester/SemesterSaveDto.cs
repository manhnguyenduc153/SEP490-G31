using System;
using PRN232_be.Helpers;

namespace PRN232_be.DTO.Semester
{
    public class SemesterSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name);
    }
}
