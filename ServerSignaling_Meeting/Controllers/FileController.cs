using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerSignaling_Meeting.Dtos.ChatDto;
using ServerSignaling_Meeting.Extensions;

namespace ServerSignaling_Meeting.Controllers
{
    [Route("api/files")]
    [ApiController]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileController> _logger;

        //  CHỈ CHO PHÉP 7 LOẠI FILE
        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif",  // Images
            ".mp4",                            // Video
            ".pdf",                            // Document
            ".docx"                            // Word
        };

        private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

        public FileController(
            IWebHostEnvironment env,
            ILogger<FileController> logger)
        {
            _env = env;
            _logger = logger;
        }
        [HttpPost("upload")]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
        {
            try
            {
                // ✅ Validate: File có tồn tại không
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No file uploaded"
                    });
                }

                // ✅ Validate: Kích thước file
                if (request.File.Length > MaxFileSize)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"File too large. Maximum size: 50MB. Your file: {request.File.Length / 1024 / 1024}MB"
                    });
                }

                // ✅ Validate: Loại file có được phép không
                var fileExtension = Path.GetExtension(request.File.FileName).ToLower();
                if (!AllowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"File type not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}"
                    });
                }

                // ✅ Validate: MIME type (double-check)
                var allowedMimeTypes = new Dictionary<string, string[]>
        {
            { ".jpg", new[] { "image/jpeg" } },
            { ".jpeg", new[] { "image/jpeg" } },
            { ".png", new[] { "image/png" } },
            { ".gif", new[] { "image/gif" } },
            { ".mp4", new[] { "video/mp4" } },
            { ".pdf", new[] { "application/pdf" } },
            { ".docx", new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } }
        };

                if (allowedMimeTypes.TryGetValue(fileExtension, out var validMimeTypes))
                {
                    if (!validMimeTypes.Contains(request.File.ContentType))
                    {
                        _logger.LogWarning($"MIME type mismatch: {request.File.ContentType} for extension {fileExtension}");
                    }
                }

                // ✅ Xác định folder theo loại file
                var folderName = fileExtension switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "images",
                    ".mp4" => "videos",
                    ".pdf" or ".docx" => "documents",
                    _ => "others"
                };

                // Tạo thư mục upload
                var uploadFolder = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", folderName);
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                // ✅ Tạo tên file unique + sanitize filename
                var originalFileName = Path.GetFileNameWithoutExtension(request.File.FileName);
                var sanitizedFileName = SanitizeFileName(originalFileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}{fileExtension}";
                var filePath = Path.Combine(uploadFolder, uniqueFileName);

                // ✅ Lưu file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                // ✅ Tạo URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fileUrl = $"{baseUrl}/uploads/{folderName}/{uniqueFileName}";

                _logger.LogInformation($"✅ File uploaded: {uniqueFileName} ({request.File.Length / 1024}KB) by user {User.GetCurrentUserId()}");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        fileUrl = fileUrl,
                        fileName = request.File.FileName,
                        fileSize = request.File.Length,
                        fileType = folderName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ File upload failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Upload failed",
                    error = ex.Message
                });
            }
        }

        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                {
                    return BadRequest(new { success = false, message = "File URL is required" });
                }

                // Parse URL
                var uri = new Uri(fileUrl);
                var relativePath = uri.AbsolutePath.TrimStart('/');
                var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, relativePath);

                // ✅ Validate: File có tồn tại không
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { success = false, message = "File not found" });
                }

                // ✅ Validate: Extension có hợp lệ không
                var extension = Path.GetExtension(filePath).ToLower();
                if (!AllowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, message = "Invalid file type" });
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var fileName = Path.GetFileName(filePath);
                var contentType = GetContentType(extension);

                _logger.LogInformation($"File downloaded: {fileName} by user {User.GetCurrentUserId()}");

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ File download failed");
                return StatusCode(500, new { success = false, message = "Download failed" });
            }
        }

        [HttpDelete]
        public IActionResult DeleteFile([FromQuery] string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                {
                    return BadRequest(new { success = false, message = "File URL is required" });
                }

                var uri = new Uri(fileUrl);
                var relativePath = uri.AbsolutePath.TrimStart('/');
                var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, relativePath);

                // ✅ Validate: Extension có hợp lệ không
                var extension = Path.GetExtension(filePath).ToLower();
                if (!AllowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, message = "Invalid file type" });
                }

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation($"✅ File deleted: {filePath} by user {User.GetCurrentUserId()}");
                    return Ok(new { success = true, message = "File deleted" });
                }

                return NotFound(new { success = false, message = "File not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ File deletion failed");
                return StatusCode(500, new { success = false, message = "Delete failed" });
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Sanitize filename để tránh path traversal attack
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));

            // Limit length
            if (sanitized.Length > 100)
            {
                sanitized = sanitized.Substring(0, 100);
            }

            return sanitized;
        }

        /// <summary>
        /// Get Content-Type dựa vào extension
        /// </summary>
        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".mp4" => "video/mp4",
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}