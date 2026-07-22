using System.ComponentModel.DataAnnotations;

namespace sep490_be.DTO.Auth
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "ERR_OLD_PASSWORD_REQUIRED")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "ERR_NEW_PASSWORD_REQUIRED")]
        [MinLength(6, ErrorMessage = "ERR_PASSWORD_MIN_LENGTH")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "ERR_CONFIRM_PASSWORD_REQUIRED")]
        [Compare("NewPassword", ErrorMessage = "ERR_PASSWORD_MISMATCH")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
