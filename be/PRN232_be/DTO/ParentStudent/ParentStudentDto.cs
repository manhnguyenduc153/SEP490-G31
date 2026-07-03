namespace PRN232_be.DTO.ParentStudent
{
    public class ParentStudentDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // Mã phụ huynh
        public string Name { get; set; } = string.Empty;   // Tên phụ huynh
        public int StudentId { get; set; }
        public string? StudentName { get; set; }            // Tên học sinh (join từ Student)
        public string? ParentPhone { get; set; }
        public string? Email { get; set; }
        public string? Relationship { get; set; }
        public int Status { get; set; }
        public string? UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
