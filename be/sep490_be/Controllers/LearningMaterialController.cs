using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.LearningMaterial;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LearningMaterialController : ControllerBase
    {
        private readonly ILearningMaterialService _service;

        public LearningMaterialController(ILearningMaterialService service)
        {
            _service = service;
        }

        [HttpGet]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_View)]
        public async Task<IActionResult> GetAll([FromQuery] LearningMaterialSearchDto searchDto)
        {
            var username = User.Identity?.Name ?? string.Empty;
            var roles = GetUserRoles();
            
            var response = await _service.GetAllMaterialsAsync(searchDto, username, roles);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetMaterialByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_Create)]
        public async Task<IActionResult> Create([FromBody] LearningMaterialSaveDto dto)
        {
            var username = User.Identity?.Name ?? string.Empty;
            var response = await _service.CreateMaterialAsync(dto, username);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] LearningMaterialSaveDto dto)
        {
            dto.Id = id;
            var username = User.Identity?.Name ?? string.Empty;
            var roles = GetUserRoles();

            var response = await _service.EditMaterialAsync(dto, username, roles);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var username = User.Identity?.Name ?? string.Empty;
            var roles = GetUserRoles();

            var response = await _service.DeleteMaterialAsync(id, username, roles);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.LearningMaterial.LearningMaterial_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var username = User.Identity?.Name ?? string.Empty;
            var roles = GetUserRoles();

            var response = await _service.DeactiveMaterialAsync(id, username, roles);
            return StatusCode(response.StatusCode, response);
        }

        private IList<string> GetUserRoles()
        {
            return User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();
        }
    }
}

