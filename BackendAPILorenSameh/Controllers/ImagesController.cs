using Asp.Versioning;
using BusinessLayer.Attributes;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPILorenSameh.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [RequireActiveLogin]
    public class ImagesController : ControllerBase
    {
        private readonly IImageStorageService _imageStorage;

        public ImagesController(IImageStorageService imageStorage)
        {
            _imageStorage = imageStorage;
        }

        /// <summary>Upload up to 5 images for a found item. Returns array of URLs.</summary>
        [HttpPost("upload")]
        [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB total
        public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
        {
            if (files == null || !files.Any())
                return BadRequest(ResponceApi<string>.Fail("No files provided."));

            if (files.Count > 5)
                return BadRequest(ResponceApi<string>.Fail("Maximum 5 images allowed per item."));

            var urls = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    var url = await _imageStorage.UploadAsync(file, "found-items", 5 * 1024 * 1024);
                    if (url != null) urls.Add(url);
                }
                catch (Exception ex)
                {
                    return BadRequest(ResponceApi<string>.Fail(ex.Message));
                }
            }

            return Ok(ResponceApi<List<string>>.Ok(urls, "Images uploaded successfully."));
        }

        /// <summary>Delete an image by URL</summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteImage([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(ResponceApi<string>.Fail("URL required."));

            var deleted = await _imageStorage.DeleteAsync(url);
            return Ok(ResponceApi<bool>.Ok(deleted, deleted ? "Deleted." : "File not found or already deleted."));
        }
    }
}
