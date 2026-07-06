using sep490_be.Helpers;
using System.ComponentModel.DataAnnotations;

namespace sep490_be.DTO.ParentStudent
{
    public class ParentStudentSaveDto
    {
        public int Id { get; set; }   // 0 = tạo mới, >0 = cập nhật

        [Required(ErrorMessage = "StudentId là bắt buộc")]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "Tên phụ huynh là bắt buộc")]
        [MaxLength(200, ErrorMessage = "Tên phụ huynh không vượt quá 200 ký tự")]
        public string Name { get; set; } = string.Empty;   // Tên phụ huynh (= Name của StandardEntity)

        [MaxLength(20, ErrorMessage = "Số điện thoại không vượt quá 20 ký tự")]
        public string? ParentPhone { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [MaxLength(150, ErrorMessage = "Email không vượt quá 150 ký tự")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Mối quan hệ không vượt quá 50 ký tự")]
        public string? Relationship { get; set; }
    }
}

