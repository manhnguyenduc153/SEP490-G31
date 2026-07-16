using System;
using System.Collections.Generic;

namespace sep490_be.DTO.ParentStudent
{
    public class ParentStudentDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // Mã phụ huynh
        public string Name { get; set; } = string.Empty;   // Tên phụ huynh
        public string? ParentPhone { get; set; }
        public string? Email { get; set; }
        public string? Relationship { get; set; }           // Mối quan hệ chung
        public int Status { get; set; }
        public string? UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        // Mối liên kết với nhiều con
        public List<ChildDto> Children { get; set; } = new List<ChildDto>();
    }

    public class ChildDto
    {
        public int StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? Relationship { get; set; }
    }
}
