using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.ServedServices;
using System.Security.Claims;
using Shed.Data;
using Shed.Services;

namespace Shed.Pages
{
    public class FilesModel : PageModel
    {
        private readonly IServeFiles _serveFiles;
        private readonly IConfiguration _configuration;
        private readonly VanillaDbContext _vanillaDbContext;
        private readonly IStorageOwnerResolver _storageOwnerResolver;

        public FilesModel(
            IServeFiles serveFiles,
            IConfiguration configuration,
            VanillaDbContext vanillaDbContext,
            IStorageOwnerResolver storageOwnerResolver)
        {
            _serveFiles = serveFiles;
            _configuration = configuration;
            _vanillaDbContext = vanillaDbContext;
            _storageOwnerResolver = storageOwnerResolver;
        }

        public IReadOnlyCollection<FileCardDto> Items { get; private set; } = Array.Empty<FileCardDto>();

        [BindProperty(SupportsGet = true)]
        public string ParentPath { get; set; } = "/";

        [BindProperty]
        public string FolderName { get; set; } = string.Empty;

        [BindProperty]
        public string DeletePath { get; set; } = string.Empty;

        [BindProperty]
        public string SourcePath { get; set; } = string.Empty;

        [BindProperty]
        public string TargetFolderPath { get; set; } = string.Empty;

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var userId = await GetCurrentUserIdAsync(cancellationToken);
            Items = (await _serveFiles.GetFileCardsAsync(ParentPath, userId, cancellationToken)).ToList();
        }

        public async Task<IActionResult> OnPostUploadAsync(List<IFormFile>? uploadFiles, CancellationToken cancellationToken)
        {
            var validFiles = (uploadFiles ?? [])
                .Where(file => file is not null && file.Length > 0)
                .ToList();

            if (validFiles.Count == 0)
            {
                return RedirectToPage(new { parentPath = ParentPath });
            }

            var storageRoot = _configuration["Storage:RootPath"]
                ?? @"/app/data/prodstorage";
            var userFolder = SanitizeFolderSegment(await GetCurrentUserIdAsync(cancellationToken));

            var normalizedParent = NormalizeParentPath(ParentPath);
            var targetDirectory = Path.Combine(storageRoot, userFolder, normalizedParent);
            Directory.CreateDirectory(targetDirectory);

            foreach (var uploadFile in validFiles)
            {
                await StoreUploadedFileAsync(uploadFile, normalizedParent, targetDirectory, cancellationToken);
            }

            return RedirectToPage(new { parentPath = ParentPath });
        }

        public async Task<IActionResult> OnPostCreateFolderAsync(CancellationToken cancellationToken)
        {
            var safeFolderName = SanitizeNewFolderSegment(FolderName);
            if (string.IsNullOrWhiteSpace(safeFolderName))
            {
                return RedirectToPage(new { parentPath = ParentPath });
            }

            var storageRoot = _configuration["Storage:RootPath"]
                ?? @"/app/data/prodstorage";
            var userFolder = SanitizeFolderSegment(await GetCurrentUserIdAsync(cancellationToken));

            var normalizedParent = NormalizeParentPath(ParentPath);
            var targetDirectory = Path.Combine(storageRoot, userFolder, normalizedParent, safeFolderName);
            Directory.CreateDirectory(targetDirectory);

            var keepFilePath = Path.Combine(targetDirectory, ".keep");
            if (!System.IO.File.Exists(keepFilePath))
            {
                await using var keepFileStream = System.IO.File.Create(keepFilePath);
                await keepFileStream.FlushAsync(cancellationToken);
            }

            var visiblePath = BuildVisiblePath(normalizedParent, safeFolderName, ".keep");
            var fileItem = new FileItem(
                Guid.NewGuid(),
                visiblePath,
                keepFilePath,
                "application/octet-stream",
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            await _serveFiles.PostProcessUploadedFile(fileItem, cancellationToken);

            return RedirectToPage(new { parentPath = ParentPath });
        }

        public async Task<IActionResult> OnPostDeleteFileAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(DeletePath))
            {
                await _serveFiles.DeleteFileByVirtualPathAsync(DeletePath, await GetCurrentUserIdAsync(cancellationToken), cancellationToken);
            }

            return RedirectToPage(new { parentPath = ParentPath });
        }

        public async Task<IActionResult> OnPostDeleteFolderAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(DeletePath))
            {
                await _serveFiles.DeleteFolderByVirtualPathAsync(DeletePath, await GetCurrentUserIdAsync(cancellationToken), cancellationToken);
            }

            return RedirectToPage(new { parentPath = ParentPath });
        }

        public async Task<IActionResult> OnPostMoveFileAsync(CancellationToken cancellationToken)
        {
            var sourcePath = NormalizeVirtualPath(SourcePath);
            var targetFolderPath = NormalizeVirtualPath(TargetFolderPath);

            if (string.IsNullOrWhiteSpace(sourcePath)
                || string.IsNullOrWhiteSpace(targetFolderPath)
                || sourcePath == "/")
            {
                return new JsonResult(new { ok = false, message = "Invalid source or target path." });
            }

            var entryName = Path.GetFileName(sourcePath);
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return new JsonResult(new { ok = false, message = "Invalid source path." });
            }

            var destinationVirtualPath = CombineVirtualPath(targetFolderPath, entryName);
            if (string.Equals(sourcePath, destinationVirtualPath, StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { ok = true });
            }

            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var storageRoot = _configuration["Storage:RootPath"]
                ?? @"/app/data/prodstorage";
            var userFolder = SanitizeFolderSegment(userId);
            var userRoot = Path.GetFullPath(Path.Combine(storageRoot, userFolder));

            string sourcePhysicalPath;
            string targetFolderPhysicalPath;
            string destinationPhysicalPath;
            try
            {
                sourcePhysicalPath = ResolvePhysicalPath(userRoot, sourcePath);
                targetFolderPhysicalPath = ResolvePhysicalPath(userRoot, targetFolderPath);
                destinationPhysicalPath = ResolvePhysicalPath(userRoot, destinationVirtualPath);
            }
            catch (InvalidOperationException)
            {
                return new JsonResult(new { ok = false, message = "Invalid source or target path." });
            }

            var sourceIsFile = System.IO.File.Exists(sourcePhysicalPath);
            var sourceIsDirectory = Directory.Exists(sourcePhysicalPath);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                return new JsonResult(new { ok = false, message = "Source item was not found." });
            }

            if (!Directory.Exists(targetFolderPhysicalPath))
            {
                return new JsonResult(new { ok = false, message = "Target folder was not found." });
            }

            if (sourceIsDirectory)
            {
                var sourcePrefix = sourcePath.TrimEnd('/') + "/";
                if (string.Equals(targetFolderPath, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || targetFolderPath.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return new JsonResult(new { ok = false, message = "Cannot move a folder into itself." });
                }
            }

            if (System.IO.File.Exists(destinationPhysicalPath) || Directory.Exists(destinationPhysicalPath))
            {
                return new JsonResult(new { ok = false, message = "An item with the same name already exists in the target folder." });
            }

            var destinationPrefix = destinationVirtualPath.TrimEnd('/') + "/";
            var destinationExistsInDb = await _vanillaDbContext.FileItems
                .AsNoTracking()
                .AnyAsync(x => x.Path == destinationVirtualPath || x.Path.StartsWith(destinationPrefix), cancellationToken);
            if (destinationExistsInDb)
            {
                return new JsonResult(new { ok = false, message = "An item with the same name already exists in the target folder." });
            }

            var now = DateTime.UtcNow;
            if (sourceIsFile)
            {
                System.IO.File.Move(sourcePhysicalPath, destinationPhysicalPath);

                var rows = await _vanillaDbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE FileItems
SET Path = {destinationVirtualPath},
    ActualPath = {destinationPhysicalPath},
    UpdatedAt = {now}
WHERE Path = {sourcePath}", cancellationToken);

                if (rows == 0)
                {
                    var inferredContentType = InferContentType(destinationPhysicalPath);
                    var fileInfo = new FileInfo(destinationPhysicalPath);

                    var fileItem = new FileItem(
                        Guid.NewGuid(),
                        destinationVirtualPath,
                        destinationPhysicalPath,
                        inferredContentType,
                        fileInfo.Length,
                        now,
                        now);

                    await _serveFiles.PostProcessUploadedFile(fileItem, cancellationToken);
                }
            }
            else
            {
                Directory.Move(sourcePhysicalPath, destinationPhysicalPath);

                var sourcePathPrefix = sourcePath.TrimEnd('/') + "/";
                var sourcePhysicalPathPrefix = sourcePhysicalPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                await _vanillaDbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE FileItems
SET Path = CASE
        WHEN Path = {sourcePath} THEN {destinationVirtualPath}
        WHEN Path LIKE {sourcePathPrefix + "%"} THEN {destinationVirtualPath} || substr(Path, {sourcePathPrefix.Length})
        ELSE Path
    END,
    ActualPath = CASE
        WHEN ActualPath = {sourcePhysicalPath} THEN {destinationPhysicalPath}
        WHEN ActualPath LIKE {sourcePhysicalPathPrefix + "%"} THEN {destinationPhysicalPath} || substr(ActualPath, {sourcePhysicalPathPrefix.Length})
        ELSE ActualPath
    END,
    UpdatedAt = {now}
WHERE Path = {sourcePath}
   OR Path LIKE {sourcePathPrefix + "%"};", cancellationToken);
            }

            return new JsonResult(new { ok = true });
        }

        private async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken)
        {
            var resolution = await _storageOwnerResolver.ResolveAsync(User, cancellationToken);
            return resolution.UserId;
        }

        private static string NormalizeParentPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                return string.Empty;
            }

            var parts = path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .ToArray();

            return Path.Combine(parts);
        }

        private static string BuildVisiblePath(string normalizedParent, string folderName, string fileName)
        {
            var relativeParent = normalizedParent.Replace('\\', '/');
            var relativePart = string.IsNullOrWhiteSpace(relativeParent)
                ? string.Empty
                : "/" + relativeParent;

            return $"{relativePart}/{folderName}/{fileName}";
        }

        private async Task StoreUploadedFileAsync(IFormFile uploadFile, string normalizedParent, string targetDirectory, CancellationToken cancellationToken)
        {
            var safeFileName = Path.GetFileName(uploadFile.FileName);
            var destination = ResolveCollisionSafePath(targetDirectory, safeFileName);
            var destinationPath = destination.PhysicalPath;
            var storedFileName = destination.FileName;

            await using (var fileStream = System.IO.File.Create(destinationPath))
            {
                await uploadFile.CopyToAsync(fileStream, cancellationToken);
            }

            var relativeParent = normalizedParent.Replace('\\', '/');
            var relativePart = string.IsNullOrWhiteSpace(relativeParent)
                ? string.Empty
                : "/" + relativeParent;
            var visiblePath = $"{relativePart}/{storedFileName}";

            var fileItem = new FileItem(
                Guid.NewGuid(),
                visiblePath,
                destinationPath,
                string.IsNullOrWhiteSpace(uploadFile.ContentType) ? "application/octet-stream" : uploadFile.ContentType,
                uploadFile.Length,
                DateTime.UtcNow,
                DateTime.UtcNow);

            await _serveFiles.PostProcessUploadedFile(fileItem, cancellationToken);
        }

        private static string SanitizeFolderSegment(string value)
        {
            var cleaned = value;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(invalid, '_');
            }

            cleaned = cleaned.Replace("/", "_").Replace("\\", "_");
            return string.IsNullOrWhiteSpace(cleaned) ? "user" : cleaned;
        }

        private static string? SanitizeNewFolderSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = value.Trim();
            if (cleaned == "." || cleaned == "..")
            {
                return null;
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(invalid, '_');
            }

            cleaned = cleaned.Replace("/", "_").Replace("\\", "_");
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }

        private static string NormalizeVirtualPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            var parts = path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .ToArray();

            return parts.Length == 0 ? "/" : "/" + string.Join('/', parts);
        }

        private static string CombineVirtualPath(string basePath, string fileName)
        {
            var normalizedBase = NormalizeVirtualPath(basePath);
            var safeFileName = Path.GetFileName(fileName);

            return normalizedBase == "/"
                ? "/" + safeFileName
                : normalizedBase.TrimEnd('/') + "/" + safeFileName;
        }

        private static string ResolvePhysicalPath(string userRoot, string virtualPath)
        {
            var normalized = NormalizeVirtualPath(virtualPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(userRoot, normalized));

            if (!fullPath.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path traversal is not allowed.");
            }

            return fullPath;
        }

        private static string InferContentType(string physicalPath)
        {
            var ext = Path.GetExtension(physicalPath).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".zip" => "application/zip",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "text/javascript",
                ".txt" or ".md" or ".log" or ".csv" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private static (string PhysicalPath, string FileName) ResolveCollisionSafePath(string targetDirectory, string fileName)
        {
            var safeFileName = string.IsNullOrWhiteSpace(fileName)
                ? "upload"
                : Path.GetFileName(fileName);

            var (baseName, extension) = SplitFileName(safeFileName);
            var candidateName = safeFileName;
            var candidatePath = Path.Combine(targetDirectory, candidateName);

            if (!System.IO.File.Exists(candidatePath) && !Directory.Exists(candidatePath))
            {
                return (candidatePath, candidateName);
            }

            for (var index = 1; ; index++)
            {
                candidateName = $"{baseName} ({index}){extension}";
                candidatePath = Path.Combine(targetDirectory, candidateName);
                if (!System.IO.File.Exists(candidatePath) && !Directory.Exists(candidatePath))
                {
                    return (candidatePath, candidateName);
                }
            }
        }

        private static (string BaseName, string Extension) SplitFileName(string fileName)
        {
            if (fileName.Length > 1 && fileName[0] == '.' && fileName.LastIndexOf('.') == 0)
            {
                return (fileName, string.Empty);
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                return (fileName, string.Empty);
            }

            var baseName = fileName[..^extension.Length];
            return string.IsNullOrEmpty(baseName)
                ? (fileName, string.Empty)
                : (baseName, extension);
        }
    }
}


