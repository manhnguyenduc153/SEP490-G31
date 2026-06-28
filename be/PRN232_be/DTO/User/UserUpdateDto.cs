using System.ComponentModel.DataAnnotations;

namespace PRN232_be.DTO.User
{
    public class UserUpdateDto
    {
        [Required(ErrorMessage = "ERR_ID_REQUIRED")]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "ERR_EMAIL_REQUIRED")]
        [EmailAddress(ErrorMessage = "ERR_EMAIL_INVALID")]
        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "ERR_ROLE_REQUIRED")]
        public string RoleName { get; set; } = string.Empty;
    }
}
