using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace UniversityEventsManagement.Services
{
    public interface IImageUploadService
    {
        Task<string> UploadEventImageAsync(IFormFile file, int eventId);
        Task<string> UploadClubLogoAsync(IFormFile file, int clubId);
        Task<bool> DeleteImageAsync(string imagePath);
        Task<bool> IsValidImageAsync(IFormFile file);
    }

    public class ImageUploadService : IImageUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageUploadService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly long _maxFileSize = 5 * 1024 * 1024; // 5 MB

        public ImageUploadService(IWebHostEnvironment environment, ILogger<ImageUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> UploadEventImageAsync(IFormFile file, int eventId)
        {
            try
            {
                if (!await IsValidImageAsync(file))
                    throw new Exception("صيغة الملف غير مدعومة أو حجم الملف كبير جداً");

                var dataUri = await ConvertToDataUriAsync(file);
                _logger.LogInformation($"Event image converted to data URI for event {eventId}");
                return dataUri;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading event image: {ex.Message}");
                throw;
            }
        }

        public async Task<string> UploadClubLogoAsync(IFormFile file, int clubId)
        {
            try
            {
                if (!await IsValidImageAsync(file))
                    throw new Exception("صيغة الملف غير مدعومة أو حجم الملف كبير جداً");

                var dataUri = await ConvertToDataUriAsync(file);
                _logger.LogInformation($"Club logo converted to data URI for club {clubId}");
                return dataUri;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading club logo: {ex.Message}");
                throw;
            }
        }

        public Task<bool> DeleteImageAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                    return Task.FromResult(false);

                if (imagePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(true);

                // Convert relative path to absolute path
                var absolutePath = imagePath.TrimStart('/');
                var fullPath = Path.Combine(_environment.WebRootPath, absolutePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation($"Image deleted successfully: {imagePath}");
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting image: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task<bool> IsValidImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            if (file.Length > _maxFileSize)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!Array.Exists(_allowedExtensions, element => element == extension))
                return false;

            // Check file signature (magic bytes)
            using (var stream = file.OpenReadStream())
            {
                byte[] buffer = new byte[4];
                var bytesRead = await stream.ReadAsync(buffer, 0, 4);
                if (bytesRead < 4)
                {
                    return false;
                }

                // Check for JPEG signature
                if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return true;
                }

                // Check for PNG signature
                if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return true;
                }

                // Check for GIF signature
                if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return true;
                }

                // Check for WebP signature
                if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return true;
                }
            }

            return false;
        }

        private async Task<string> ConvertToDataUriAsync(IFormFile file)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var contentType = !string.IsNullOrWhiteSpace(file.ContentType)
                    ? file.ContentType
                    : GetContentTypeFromExtension(Path.GetExtension(file.FileName));

                var base64 = Convert.ToBase64String(memoryStream.ToArray());
                return $"data:{contentType};base64,{base64}";
            }
        }

        private static string GetContentTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
