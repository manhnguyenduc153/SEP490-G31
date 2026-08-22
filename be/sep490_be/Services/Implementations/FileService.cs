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
            var cloudName = configuration["Cloudinary:CloudName"]
                ?? configuration["Cloudinary__CloudName"]
                ?? configuration["CLOUDINARY_CLOUD_NAME"]
                ?? configuration["Cloudinary_CloudName"]
                ?? Environment.GetEnvironmentVariable("Cloudinary__CloudName")
                ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME")
                ?? Environment.GetEnvironmentVariable("Cloudinary_CloudName");

            var apiKey = configuration["Cloudinary:ApiKey"]
                ?? configuration["Cloudinary__ApiKey"]
                ?? configuration["CLOUDINARY_API_KEY"]
                ?? configuration["Cloudinary_ApiKey"]
                ?? Environment.GetEnvironmentVariable("Cloudinary__ApiKey")
                ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")
                ?? Environment.GetEnvironmentVariable("Cloudinary_ApiKey");

            var apiSecret = configuration["Cloudinary:ApiSecret"]
                ?? configuration["Cloudinary__ApiSecret"]
                ?? configuration["CLOUDINARY_API_SECRET"]
                ?? configuration["Cloudinary_ApiSecret"]
                ?? Environment.GetEnvironmentVariable("Cloudinary__ApiSecret")
                ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET")
                ?? Environment.GetEnvironmentVariable("Cloudinary_ApiSecret");

            if (!string.IsNullOrWhiteSpace(cloudName) && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
            {
                var account = new Account(cloudName.Trim(), apiKey.Trim(), apiSecret.Trim());
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
                throw new ArgumentException("ERR_NO_FILE_UPLOADED");

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            if (file.Length > maxFileSize)
                throw new ArgumentException("ERR_FILE_SIZE_EXCEEDS_10MB");

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
                if (uploadResult.Error != null)
                {
                    throw new Exception("ERR_FILE_UPLOAD_FAILED");
                }

                var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new Exception("ERR_FILE_UPLOAD_FAILED");
                }

                return url;
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
                if (uploadResult.Error != null)
                {
                    throw new Exception("ERR_FILE_UPLOAD_FAILED");
                }

                var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new Exception("ERR_FILE_UPLOAD_FAILED");
                }

                return url;
            }
        }
    }
}


