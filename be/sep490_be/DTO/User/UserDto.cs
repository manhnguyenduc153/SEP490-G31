using System.Collections.Generic;

namespace sep490_be.DTO.User
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public int Status { get; set; } // 1: Active, 0: Inactive
    }
}

