using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using sep490_be.Services.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sep490_be.Services.Implementations
{
    public class FileService : IFileService
    {
        private readonly Cloudinary _cloudinary;

        public FileService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
                _cloudinary.Api.Secure = true;
            }
            else
            {
                _cloudinary = null!;
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null.");

            if (_cloudinary == null)
                throw new InvalidOperationException("Cloudinary configuration is missing. Please configure Cloudinary:CloudName, ApiKey, and ApiSecret.");

            using var stream = file.OpenReadStream();
            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            var ext = Path.GetExtension(file.FileName).ToLower();

            var isImage = ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".webp" || ext == ".gif" || ext == ".svg";

            if (isImage)
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"sep490/{folderName}",
                    PublicId = $"{fileName}_{Guid.NewGuid():N}"
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? "";
            }
            else
            {
                var rawUploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"sep490/{folderName}",
                    PublicId = $"{fileName}_{Guid.NewGuid():N}{ext}"
                };
                var uploadResult = await _cloudinary.UploadAsync(rawUploadParams);
                return uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? "";
            }
        }
    }
}


