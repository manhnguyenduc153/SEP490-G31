using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using sep490_be.Helpers;
using sep490_be.DTO;
using sep490_be.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IFileService _fileService;

        public UploadController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("No file uploaded."));
                }

                // Check file extension if needed
                var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                if (ext != ".jpg" && ext != ".png" && ext != ".jpeg")
                {
                    return BadRequest(ApiResponse<string>.Fail("Only .jpg, .jpeg, .png are allowed."));
                }

                var path = await _fileService.UploadFileAsync(file, "images");
                return Ok(ApiResponse<string>.Ok(path, "File uploaded successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        [HttpPost("document")]
        public async Task<IActionResult> UploadDocument(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("No file uploaded."));
                }

                var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                var allowed = new[] { ".jpg", ".png", ".jpeg", ".pdf", ".doc", ".docx", ".mp3", ".wav", ".ogg", ".mp4" };
                if (!Array.Exists(allowed, e => e == ext))
                {
                    return BadRequest(ApiResponse<string>.Fail("File format is not allowed."));
                }

                var path = await _fileService.UploadFileAsync(file, "documents");
                return Ok(ApiResponse<string>.Ok(path, "Document uploaded successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }
    }
}

