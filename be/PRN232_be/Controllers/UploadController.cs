using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PRN232_be.Helpers;
using PRN232_be.DTO;
using PRN232_be.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace PRN232_be.Controllers
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
                if (ext != ".jpg" && ext != ".png" && ext != ".jpeg" && ext != ".pdf" && ext != ".doc" && ext != ".docx")
                {
                    return BadRequest(ApiResponse<string>.Fail("Only .jpg, .jpeg, .png, .pdf, .doc, .docx are allowed."));
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
