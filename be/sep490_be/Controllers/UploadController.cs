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
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        private readonly IFileService _fileService;

        public UploadController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost("image")]
        [RequestSizeLimit(15 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_NO_FILE_UPLOADED"));
                }

                if (file.Length > MaxFileSize)
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_FILE_SIZE_EXCEEDS_10MB"));
                }

                var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                if (ext != ".jpg" && ext != ".png" && ext != ".jpeg" && ext != ".webp")
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_UPLOAD_IMAGE_FORMAT_INVALID"));
                }

                var path = await _fileService.UploadFileAsync(file, "images");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_FILE_UPLOAD_FAILED"));
                }

                return Ok(ApiResponse<string>.Ok(path, "MSG_IMAGE_UPLOAD_SUCCESS"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(ex.Message.StartsWith("ERR_") ? ex.Message : "ERR_INTERNAL_SERVER_ERROR", 500));
            }
        }

        [HttpPost("document")]
        [RequestSizeLimit(15 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
        public async Task<IActionResult> UploadDocument(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_NO_FILE_UPLOADED"));
                }

                if (file.Length > MaxFileSize)
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_FILE_SIZE_EXCEEDS_10MB"));
                }

                var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                var allowed = new[] { ".jpg", ".png", ".jpeg", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".txt", ".csv", ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".webm", ".mkv" };
                if (!Array.Exists(allowed, e => e == ext))
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_FILE_FORMAT_NOT_ALLOWED"));
                }

                var path = await _fileService.UploadFileAsync(file, "documents");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return BadRequest(ApiResponse<string>.Fail("ERR_FILE_UPLOAD_FAILED"));
                }

                return Ok(ApiResponse<string>.Ok(path, "MSG_DOCUMENT_UPLOAD_SUCCESS"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(ex.Message.StartsWith("ERR_") ? ex.Message : "ERR_INTERNAL_SERVER_ERROR", 500));
            }
        }
    }
}

