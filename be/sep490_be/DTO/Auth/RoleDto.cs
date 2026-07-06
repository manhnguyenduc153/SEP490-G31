using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Auth
{
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
    }
}

