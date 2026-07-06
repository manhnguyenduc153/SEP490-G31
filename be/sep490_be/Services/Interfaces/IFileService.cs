using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace sep490_be.Services.Interfaces
{
    public interface IFileService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }
}

