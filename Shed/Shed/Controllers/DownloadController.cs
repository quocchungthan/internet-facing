using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.ServedServices;
using System.IO.Compression;
using System.Security.Claims;
using Shed.Data;
using Shed.Services;

namespace Shed.Controllers
{
    [ApiController]
    [Route("api/download")]
    public class DownloadController : ControllerBase
    {
        private readonly ShareLinkService _shareLinkService;
        private readonly IServeFiles _serveFiles;
        private readonly IConfiguration _configuration;
        private readonly VanillaDbContext _dbContext;
        private readonly ILogger<DownloadController> _logger;
        private readonly IStorageOwnerResolver _storageOwnerResolver;

        public DownloadController(
            ShareLinkService shareLinkService,
            IServeFiles serveFiles,
            IConfiguration configuration,
            VanillaDbContext dbContext,
            ILogger<DownloadController> logger,
            IStorageOwnerResolver storageOwnerResolver)
        {
            _shareLinkService = shareLinkService;
            _serveFiles = serveFiles;
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
            _storageOwnerResolver = storageOwnerResolver;
        }

        /// <summary>
        /// Download a file using a share link.
        /// GET /api/download/file?key=xxxxx&path=/path/to/file
        /// </summary>
        [HttpGet("file")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadFileAsync(
            [FromQuery] string key,
            [FromQuery] string path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path))
                return BadRequest("key and path required");

            // Validate share link
            var validation = await _shareLinkService.ValidateShareLinkAsync(key, cancellationToken);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid share link attempt: {Reason}", validation.Reason);
                return Unauthorized(new { reason = validation.Reason });
            }

            // Check if requested path matches allowed path
            if (!PathMatches(path, validation.TargetPath))
                return BadRequest("Path not allowed for this share link");

            try
            {
                var storageRoot = _configuration["Storage:RootPath"]
                    ?? @"/app/data/prodstorage";

                // Extract owner from validation.AccessKey or get from database
                var accessKeyRecord = await _dbContext.AccessKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == validation.AccessKey, cancellationToken);

                if (accessKeyRecord == null)
                    return NotFound("Access key not found");

                var userId = SanitizeFolderSegment(accessKeyRecord.UserId);
                var normalizedPath = NormalizeParentPath(path);
                var fullPath = Path.Combine(storageRoot, userId, normalizedPath);

                // Security: ensure path is within storage
                var realPath = Path.GetFullPath(fullPath);
                var storagePath = Path.GetFullPath(Path.Combine(storageRoot, userId));
                if (!realPath.StartsWith(storagePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Path traversal attempt: {Path}", path);
                    return BadRequest("Invalid path");
                }

                if (!System.IO.File.Exists(realPath))
                    return NotFound("File not found");

                var fileName = Path.GetFileName(realPath);
                var contentType = GetContentType(realPath);

                _logger.LogInformation("File download via share link: {Path}", path);

                return PhysicalFile(realPath, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file via share link");
                return StatusCode(500, "Download failed");
            }
        }

        /// <summary>
        /// Download a folder as zip using a share link.
        /// GET /api/download/folder?key=xxxxx&path=/path/to/folder
        /// </summary>
        [HttpGet("folder")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadFolderAsync(
            [FromQuery] string key,
            [FromQuery] string path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path))
                return BadRequest("key and path required");

            // Validate share link
            var validation = await _shareLinkService.ValidateShareLinkAsync(key, cancellationToken);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid share link attempt: {Reason}", validation.Reason);
                return Unauthorized(new { reason = validation.Reason });
            }

            // Check if requested path matches allowed path
            if (!PathMatches(path, validation.TargetPath))
                return BadRequest("Path not allowed for this share link");

            try
            {
                var storageRoot = _configuration["Storage:RootPath"]
                    ?? @"/app/data/prodstorage";

                var accessKeyRecord = await _dbContext.AccessKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == validation.AccessKey, cancellationToken);

                if (accessKeyRecord == null)
                    return NotFound("Access key not found");

                var userId = SanitizeFolderSegment(accessKeyRecord.UserId);
                var normalizedPath = NormalizeParentPath(path);
                var folderPath = Path.Combine(storageRoot, userId, normalizedPath);

                // Security: ensure path is within storage
                var realPath = Path.GetFullPath(folderPath);
                var storagePath = Path.GetFullPath(Path.Combine(storageRoot, userId));
                if (!realPath.StartsWith(storagePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Path traversal attempt: {Path}", path);
                    return BadRequest("Invalid path");
                }

                if (!Directory.Exists(realPath))
                    return NotFound("Folder not found");

                // Stream zip to client without storing on disk
                var zipFileName = $"{Path.GetFileName(realPath.TrimEnd('\\'))}.zip";
                var memoryStream = new MemoryStream();

                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    AddFolderToZip(archive, realPath, "", cancellationToken);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);

                _logger.LogInformation("Folder download via share link: {Path}", path);

                return File(memoryStream, "application/zip", zipFileName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Folder download cancelled");
                return StatusCode(499, "Download cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading folder via share link");
                return StatusCode(500, "Download failed");
            }
        }

        /// <summary>
        /// Create a share link for a file or folder and return an absolute download URL.
        /// POST /api/download/share-link
        /// </summary>
        [HttpPost("share-link")]
        public async Task<IActionResult> CreateShareLinkAsync(
            [FromBody] CreateShareLinkRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TargetPath))
            {
                return BadRequest(new { error = "targetPath required" });
            }

            if (!string.Equals(request.TargetType, "file", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.TargetType, "folder", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "targetType must be 'file' or 'folder'" });
            }

            var normalizedType = string.Equals(request.TargetType, "folder", StringComparison.OrdinalIgnoreCase)
                ? "folder"
                : "file";

            var expirationMinutes = request.ExpirationMinutes.GetValueOrDefault(1440);
            if (expirationMinutes <= 0)
            {
                return BadRequest(new { error = "expirationMinutes must be greater than 0" });
            }

            try
            {
                var userId = await GetCurrentUserIdAsync(cancellationToken);
                var link = await _shareLinkService.CreateShareLinkAsync(
                    userId,
                    request.TargetPath,
                    expirationMinutes,
                    cancellationToken);

                var relativeUrl = $"/api/download/{normalizedType}?key={Uri.EscapeDataString(link.AccessKey)}&path={Uri.EscapeDataString(link.TargetPath)}";
                var absoluteUrl = BuildAbsoluteShareUrl(relativeUrl);

                return Ok(new
                {
                    shareUrl = absoluteUrl,
                    relativeUrl,
                    accessKey = link.AccessKey,
                    targetPath = link.TargetPath,
                    targetType = normalizedType,
                    expiresAt = link.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating share link for {Path}", request.TargetPath);
                return StatusCode(500, new { error = "Failed to create share link" });
            }
        }

        /// <summary>
        /// Get share links for a specific path.
        /// GET /api/download/share-links?path=/path/to/item
        /// </summary>
        [HttpGet("share-links")]
        public async Task<IActionResult> GetShareLinksAsync(
            [FromQuery] string path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("path required");

            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var links = await _shareLinkService.GetShareLinksForPathAsync(userId, path, cancellationToken);

            return Ok(new { shareLinks = links });
        }

        /// <summary>
        /// Get all share links for current user.
        /// GET /api/download/my-share-links
        /// </summary>
        [HttpGet("my-share-links")]
        public async Task<IActionResult> GetMyShareLinksAsync(CancellationToken cancellationToken)
        {
            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var links = await _shareLinkService.GetShareLinksAsync(userId, cancellationToken);
            return Ok(new { shareLinks = links });
        }

        private void AddFolderToZip(ZipArchive archive, string folderPath, string folderPrefix, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = new DirectoryInfo(folderPath);

            // Add files
            foreach (var file in directory.GetFiles())
            {
                var entryName = string.IsNullOrEmpty(folderPrefix)
                    ? file.Name
                    : Path.Combine(folderPrefix, file.Name).Replace("\\", "/");

                var entry = archive.CreateEntry(entryName);
                using (var entryStream = entry.Open())
                using (var fileStream = System.IO.File.OpenRead(file.FullName))
                {
                    fileStream.CopyTo(entryStream);
                }
            }

            // Add subdirectories recursively
            foreach (var subdir in directory.GetDirectories())
            {
                var subfolderPrefix = string.IsNullOrEmpty(folderPrefix)
                    ? subdir.Name
                    : Path.Combine(folderPrefix, subdir.Name).Replace("\\", "/");

                AddFolderToZip(archive, subdir.FullName, subfolderPrefix, cancellationToken);
            }
        }

        private bool PathMatches(string requestedPath, string allowedPath)
        {
            // Simple path matching: requested must be under allowed
            var normalized1 = NormalizeParentPath(requestedPath);
            var normalized2 = NormalizeParentPath(allowedPath);

            return normalized1 == normalized2 || normalized1.StartsWith(normalized2 + "/");
        }

        private string NormalizeParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/")
                return "";

            return path.Trim('/').Replace("\\", "/");
        }

        private string SanitizeFolderSegment(string segment)
        {
            return System.Text.RegularExpressions.Regex.Replace(segment, @"[^a-zA-Z0-9_-]", "");
        }

        private string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        private async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken)
        {
            var resolution = await _storageOwnerResolver.ResolveAsync(User, cancellationToken);
            return resolution.UserId;
        }

        private string BuildAbsoluteShareUrl(string relativeUrl)
        {
            var host = Request.Host.Value;

            if (Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
            {
                var firstForwardedHost = forwardedHost
                    .ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(firstForwardedHost))
                {
                    host = firstForwardedHost;
                }
            }

            return $"{Uri.UriSchemeHttps}://{host}{relativeUrl}";
        }

        public class CreateShareLinkRequest
        {
            public string TargetPath { get; set; } = string.Empty;
            public string TargetType { get; set; } = "file";
            public int? ExpirationMinutes { get; set; }
        }
    }
}


