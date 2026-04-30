using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services
{
    public interface IImageStorageService
    {
        Task<string?> UploadAsync(IFormFile file, string folder, long maxBytes = 5 * 1024 * 1024);
        Task<string?> ReplaceAsync(IFormFile newFile, string folder, string? oldUrl, long maxBytes = 5 * 1024 * 1024);
        Task<bool> DeleteAsync(string? url);

        bool IsLocalUploadUrl(string? url);
    }

    public class ImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImageStorageService> _logger;

        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public bool IsLocalUploadUrl(string? url)
            => !string.IsNullOrWhiteSpace(url) && url.Contains("/uploads/", StringComparison.OrdinalIgnoreCase);

        public async Task<string?> UploadAsync(IFormFile file, string folder, long maxBytes = 5 * 1024 * 1024)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExt.Contains(ext))
                throw new Exception("Invalid file type. Only images are allowed.");

            if (file.Length > maxBytes)
                throw new Exception("File size exceeds limit.");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{folder}/{fileName}";
        }

        public async Task<string?> ReplaceAsync(IFormFile newFile, string folder, string? oldUrl, long maxBytes = 5 * 1024 * 1024)
        {
            // delete old
            await DeleteAsync(oldUrl);

            // upload new
            return await UploadAsync(newFile, folder, maxBytes);
        }

        public async Task<bool> DeleteAsync(string? url)
        {
            try
            {
                if (!IsLocalUploadUrl(url)) return false;

                var relative = NormalizeLocalPath(url!);
                var fullPath = Path.Combine(_env.WebRootPath, relative);

                if (!File.Exists(fullPath)) return false;

                File.Delete(fullPath);
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAsync failed for {Url}", url);
                return false;
            }
        }

        private static string NormalizeLocalPath(string url)
        {
            var path = url;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(path);
                path = uri.AbsolutePath;
            }

            path = path.TrimStart('/');
            return path;
        }
    }
}