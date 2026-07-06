namespace sep490_be.DTO.Auth
{
    public class AssignRolePermissionsDto
    {
        public string RoleName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }
}

