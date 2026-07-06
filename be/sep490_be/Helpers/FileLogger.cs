using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace sep490_be.Helpers
{
    public static class FileLogger
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "error_log.txt");

        public static async Task LogErrorAsync(string type, string method, string path, string details)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {method} {path}\nDetails: {details}\n------------------------------------------------------------\n";
            
            await _semaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(LogFilePath, logMessage);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}

