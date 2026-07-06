using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace sep490_be.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Token { get; set; } = string.Empty;
        
        [Required]
        public string JwtId { get; set; } = string.Empty; // Để liên kết với Jti của Access Token
        
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        
        public DateTime AddedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [ForeignKey(nameof(UserId))]
        public virtual IdentityUser User { get; set; } = null!;
    }
}

