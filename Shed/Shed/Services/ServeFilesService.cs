using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.Meta;
using OpensourceLab.FileStorage.ServedServices;
using System.Security.Cryptography;
using System.Text;
using Shed.Data;

namespace Shed.Services
{
    public class ServeFilesService : IServeFiles
    {
        private static readonly string[] LegacySamplePaths =
        {
            "/public/trail-overview.jpg",
            "/projects/q3/plan.pdf",
            "/api/logs/request-log.json"
        };

        private readonly VanillaDbContext _dbContext;
        private readonly string _storageRoot;

        public ServeFilesService(VanillaDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _storageRoot = configuration["Storage:RootPath"]
                ?? @"/app/data/prodstorage";
        }

        public async Task<IEnumerable<FileItem>> GetFilesAsync(string parentPath, CancellationToken cancellationToken)
        {
            var normalizedPath = NormalizePath(parentPath);
            return await _dbContext.FileItems
                .AsNoTracking()
                .Where(x => x.Path.StartsWith(normalizedPath))
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<FileCardDto>> GetFileCardsAsync(string parentPath, string userId, CancellationToken cancellationToken)
        {
            await RemoveLegacySampleFilesAsync(cancellationToken);

            var normalizedPath = NormalizePath(parentPath);
            var userRoot = Path.Combine(_storageRoot, SanitizeFolderSegment(userId));
            var showHiddenFiles = await IsShowHiddenFilesEnabledAsync(userId, cancellationToken);

            var files = await _dbContext.FileItems
                .AsNoTracking()
                .Where(x => x.ActualPath.StartsWith(userRoot))
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(cancellationToken);

            var localizedFiles = files
                .Select(file => new
                {
                    File = file,
                    LocalizedPath = NormalizePath(LocalizePathForCurrentUser(file.Path, file.ActualPath, userRoot))
                })
                .Where(x => IsPathWithinParent(x.LocalizedPath, normalizedPath))
                .ToList();

            var fileIds = localizedFiles.Select(x => x.File.Id).ToList();
            var aclMap = await _dbContext.Acls
                .AsNoTracking()
                .Where(x => fileIds.Contains(x.FileItemId))
                .GroupBy(x => x.FileItemId)
                .Select(g => g.OrderByDescending(a => a.UpdatedAt).First())
                .ToDictionaryAsync(x => x.FileItemId, cancellationToken);

            var directFiles = new List<FileCardDto>();
            var directFolders = new Dictionary<string, FileCardDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in localizedFiles)
            {
                var file = item.File;
                var relativePath = GetRelativePath(item.LocalizedPath, normalizedPath);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                var separatorIndex = relativePath.IndexOf('/');
                var hasAcl = aclMap.TryGetValue(file.Id, out var acl);
                var ownerType = hasAcl ? acl!.Owner.Type : OwnerType.Public;
                var ownerId = hasAcl ? acl!.Owner.OwnerId : string.Empty;
                var permission = hasAcl ? acl!.Permissions : AccessLevel.Read;

                if (separatorIndex >= 0)
                {
                    var folderName = relativePath[..separatorIndex];
                    if (ShouldHideInListing(folderName, showHiddenFiles))
                    {
                        continue;
                    }

                    var folderPath = CombineVirtualPath(normalizedPath, folderName);
                    if (directFolders.TryGetValue(folderPath, out var existingFolder))
                    {
                        if (file.UpdatedAt > existingFolder.UpdatedAt)
                        {
                            directFolders[folderPath] = existingFolder with
                            {
                                UpdatedAt = file.UpdatedAt
                            };
                        }

                        continue;
                    }

                    directFolders[folderPath] = new FileCardDto(
                        CreateDeterministicGuid("folder:" + folderPath),
                        folderPath,
                        "inode/directory",
                        "/images/file-card-placeholder.svg",
                        ownerType == OwnerType.Public,
                        ownerType,
                        ownerId,
                        permission,
                        file.UpdatedAt);

                    continue;
                }

                if (ShouldHideInListing(relativePath, showHiddenFiles))
                {
                    continue;
                }

                directFiles.Add(new FileCardDto(
                    file.Id,
                    item.LocalizedPath,
                    file.ContentType,
                    ResolveThumbnail(file),
                    ownerType == OwnerType.Public,
                    ownerType,
                    ownerId,
                    permission,
                    file.UpdatedAt));
            }

            // Also include filesystem items not tracked in DB (e.g. .settings created by preferences service)
            var physicalParentDir = normalizedPath == "/"
                ? userRoot
                : Path.Combine(userRoot, normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (Directory.Exists(physicalParentDir))
            {
                foreach (var dir in Directory.GetDirectories(physicalParentDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (ShouldHideInListing(dirName, showHiddenFiles)) continue;

                    var virtualPath = CombineVirtualPath(normalizedPath, dirName);
                    if (!directFolders.ContainsKey(virtualPath))
                    {
                        var lastWrite = Directory.GetLastWriteTimeUtc(dir);
                        directFolders[virtualPath] = new FileCardDto(
                            CreateDeterministicGuid("fs-folder:" + virtualPath),
                            virtualPath,
                            "inode/directory",
                            "/images/file-card-placeholder.svg",
                            true,
                            OwnerType.Public,
                            string.Empty,
                            AccessLevel.Read,
                            lastWrite);
                    }
                }

                foreach (var file in Directory.GetFiles(physicalParentDir))
                {
                    var fileName = Path.GetFileName(file);
                    if (ShouldHideInListing(fileName, showHiddenFiles)) continue;

                    var virtualPath = CombineVirtualPath(normalizedPath, fileName);
                    // Only add if not already in DB results
                    if (directFiles.All(f => !string.Equals(f.Path, virtualPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        var ct = GetMimeTypeByExtension(fileName);
                        directFiles.Add(new FileCardDto(
                            CreateDeterministicGuid("fs-file:" + virtualPath),
                            virtualPath,
                            ct,
                            ResolveThumbnailByMime(ct, null),
                            true,
                            OwnerType.Public,
                            string.Empty,
                            AccessLevel.Read,
                            lastWrite));
                    }
                }
            }

            return directFolders.Values
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Concat(directFiles.OrderByDescending(x => x.UpdatedAt))
                .ToList();
        }

        public async Task UpsertFilePermissionsAsync(FilePermissionUpsertRequest request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var acl = await _dbContext.Acls
                .FirstOrDefaultAsync(x => x.FileItemId == request.FileItemId && x.Owner.Type == request.OwnerType && x.Owner.OwnerId == request.OwnerId, cancellationToken);

            if (acl is null)
            {
                acl = new ACL(
                    Guid.NewGuid(),
                    request.FileItemId,
                    new AclOwner(request.OwnerType, request.OwnerId),
                    request.PermissionLevel,
                    now,
                    now);
                await _dbContext.Acls.AddAsync(acl, cancellationToken);
            }
            else
            {
                acl = acl with
                {
                    Permissions = request.PermissionLevel,
                    UpdatedAt = now
                };
                _dbContext.Acls.Update(acl);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task PostProcessUploadedFile(FileItem fileItem, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var existing = await _dbContext.FileItems
                .FirstOrDefaultAsync(x => x.Id == fileItem.Id || x.Path == fileItem.Path, cancellationToken);

            if (existing is null)
            {
                await _dbContext.FileItems.AddAsync(fileItem with { CreatedAt = now, UpdatedAt = now }, cancellationToken);
            }
            else
            {
                var updated = existing with
                {
                    Path = fileItem.Path,
                    ActualPath = fileItem.ActualPath,
                    ContentType = fileItem.ContentType,
                    Size = fileItem.Size,
                    UpdatedAt = now
                };
                _dbContext.FileItems.Update(updated);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteFileByVirtualPathAsync(string virtualPath, string userId, CancellationToken cancellationToken)
        {
            var normalizedPath = NormalizePath(virtualPath);
            if (normalizedPath == "/")
            {
                return;
            }

            var userRoot = GetUserRootPath(userId);
            var files = await _dbContext.FileItems
                .Where(x => x.ActualPath.StartsWith(userRoot))
                .ToListAsync(cancellationToken);

            var target = files
                .FirstOrDefault(file => string.Equals(
                    NormalizePath(LocalizePathForCurrentUser(file.Path, file.ActualPath, userRoot)),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return;
            }

            await DeleteAclsForFilesAsync(new[] { target.Id }, cancellationToken);
            _dbContext.FileItems.Remove(target);
            await _dbContext.SaveChangesAsync(cancellationToken);

            TryDeletePhysicalFile(target.ActualPath, userRoot);
            TryDeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(target.ActualPath), userRoot);
        }

        public async Task DeleteFolderByVirtualPathAsync(string virtualFolderPath, string userId, CancellationToken cancellationToken)
        {
            var normalizedFolderPath = NormalizePath(virtualFolderPath);
            if (normalizedFolderPath == "/")
            {
                return;
            }

            var userRoot = GetUserRootPath(userId);
            var files = await _dbContext.FileItems
                .Where(x => x.ActualPath.StartsWith(userRoot))
                .ToListAsync(cancellationToken);

            var filesInFolder = files
                .Where(file =>
                {
                    var localizedPath = NormalizePath(LocalizePathForCurrentUser(file.Path, file.ActualPath, userRoot));
                    return localizedPath.StartsWith(normalizedFolderPath + "/", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (filesInFolder.Count == 0)
            {
                return;
            }

            await DeleteAclsForFilesAsync(filesInFolder.Select(x => x.Id), cancellationToken);
            _dbContext.FileItems.RemoveRange(filesInFolder);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var actualPath in filesInFolder
                .Select(x => x.ActualPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                TryDeletePhysicalFile(actualPath, userRoot);
            }

            var folderPhysicalPath = BuildPhysicalPathFromVirtualPath(normalizedFolderPath, userRoot);
            if (!string.IsNullOrWhiteSpace(folderPhysicalPath)
                && IsPathWithinRoot(folderPhysicalPath, userRoot)
                && Directory.Exists(folderPhysicalPath))
            {
                try
                {
                    Directory.Delete(folderPhysicalPath, true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }

            TryDeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(folderPhysicalPath), userRoot);
        }

        private static string NormalizePath(string parentPath)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return "/";
            }

            var parts = parentPath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .ToArray();

            if (parts.Length == 0)
            {
                return "/";
            }

            return "/" + string.Join('/', parts);
        }

        private string GetUserRootPath(string userId)
        {
            return Path.Combine(_storageRoot, SanitizeFolderSegment(userId));
        }

        private static string BuildPhysicalPathFromVirtualPath(string virtualPath, string userRoot)
        {
            var normalized = NormalizePath(virtualPath);
            var parts = normalized
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .ToArray();

            if (parts.Length == 0)
            {
                return userRoot;
            }

            var combined = parts.Aggregate(userRoot, Path.Combine);
            return Path.GetFullPath(combined);
        }

        private static bool IsPathWithinRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void TryDeletePhysicalFile(string? actualPath, string userRoot)
        {
            if (string.IsNullOrWhiteSpace(actualPath) || !IsPathWithinRoot(actualPath, userRoot))
            {
                return;
            }

            if (!File.Exists(actualPath))
            {
                return;
            }

            try
            {
                File.Delete(actualPath);
            }
            catch
            {
                // DB deletion is source of truth; filesystem deletion is best effort.
            }
        }

        private static void TryDeleteEmptyDirectoriesUpToRoot(string? startDirectory, string root)
        {
            if (string.IsNullOrWhiteSpace(startDirectory) || string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var current = startDirectory;
            while (!string.IsNullOrWhiteSpace(current)
                && IsPathWithinRoot(current, root)
                && !string.Equals(
                    Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(current))
                    {
                        break;
                    }

                    if (Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        break;
                    }

                    Directory.Delete(current, false);
                }
                catch
                {
                    break;
                }

                current = Path.GetDirectoryName(current);
            }
        }

        private async Task DeleteAclsForFilesAsync(IEnumerable<Guid> fileIds, CancellationToken cancellationToken)
        {
            var ids = fileIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var acls = await _dbContext.Acls
                .Where(x => ids.Contains(x.FileItemId))
                .ToListAsync(cancellationToken);

            if (acls.Count == 0)
            {
                return;
            }

            _dbContext.Acls.RemoveRange(acls);
        }

        private const string ThumbnailBase = "/ProdStorage/00000000-0000-0000-0000-000000000000/.settings/thumbnails";

        private static string ResolveThumbnail(FileItem fileItem)
        {
            return ResolveThumbnailByMime(fileItem.ContentType ?? string.Empty, fileItem.Id);
        }

        private static string ResolveThumbnailByMime(string ct, Guid? imageFileId)
        {
            if (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && imageFileId.HasValue)
                return $"/file-content/{imageFileId.Value}";

            if (ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-pdf.svg";

            if (ct.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-audio.svg";

            if (ct.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-video.svg";

            if (ct.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("excel", StringComparison.OrdinalIgnoreCase)
                || ct.Equals("text/csv", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-sheet.svg";

            if (ct.Contains("word", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("presentation", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("powerpoint", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("opendocument", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-doc.svg";

            if (ct.Contains("zip", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("gzip", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("tar", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("7z", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("rar", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("compress", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-archive.svg";

            if (ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && (ct.Contains("javascript", StringComparison.OrdinalIgnoreCase)
                    || ct.Contains("typescript", StringComparison.OrdinalIgnoreCase)
                    || ct.Contains("html", StringComparison.OrdinalIgnoreCase)
                    || ct.Contains("css", StringComparison.OrdinalIgnoreCase)
                    || ct.Contains("xml", StringComparison.OrdinalIgnoreCase)
                    || ct.Contains("json", StringComparison.OrdinalIgnoreCase)))
                return $"{ThumbnailBase}/file-icon-code.svg";

            if (ct.Contains("json", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("javascript", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-code.svg";

            if (ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return $"{ThumbnailBase}/file-icon-text.svg";

            return $"{ThumbnailBase}/file-card-placeholder.svg";
        }

        private static string GetMimeTypeByExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".mp3" or ".ogg" or ".wav" or ".flac" => "audio/mpeg",
                ".mp4" or ".webm" or ".mkv" or ".avi" => "video/mp4",
                ".zip" => "application/zip",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".7z" => "application/x-7z-compressed",
                ".rar" => "application/x-rar-compressed",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                ".doc" or ".docx" => "application/msword",
                ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "text/javascript",
                ".ts" => "text/typescript",
                ".txt" or ".md" or ".log" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private static string LocalizePathForCurrentUser(string path, string actualPath, string userRoot)
        {
            if (!string.IsNullOrWhiteSpace(actualPath))
            {
                var relativeFromUserRoot = Path.GetRelativePath(userRoot, actualPath);
                if (!string.IsNullOrWhiteSpace(relativeFromUserRoot)
                    && relativeFromUserRoot != "."
                    && !relativeFromUserRoot.StartsWith(".."))
                {
                    return "/" + relativeFromUserRoot.Replace('\\', '/').TrimStart('/');
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            var normalized = path.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(normalized)
                || normalized.Contains(":/")
                || normalized.Contains("ProdStorage", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(normalized);
                return string.IsNullOrWhiteSpace(fileName) ? "/" : "/" + fileName;
            }

            return NormalizePath(normalized);
        }

        private static bool IsPathWithinParent(string localizedPath, string parentPath)
        {
            if (parentPath == "/")
            {
                return localizedPath.StartsWith("/", StringComparison.OrdinalIgnoreCase);
            }

            return localizedPath.StartsWith(parentPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string localizedPath, string parentPath)
        {
            if (parentPath == "/")
            {
                return localizedPath.TrimStart('/');
            }

            var prefix = parentPath + "/";
            if (!localizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return localizedPath[prefix.Length..];
        }

        private static string CombineVirtualPath(string parentPath, string childName)
        {
            if (parentPath == "/")
            {
                return "/" + childName;
            }

            return parentPath + "/" + childName;
        }

        private static bool IsHiddenName(string name)
        {
            // Files/folders starting with dot are hidden, except we treat .settings specially below
            return name.StartsWith(".", StringComparison.Ordinal) || string.Equals(name, ".keep", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a name should be treated as hidden in the file listing.
        /// When ShowHiddenFiles is false, hides dot-files and .keep.
        /// When ShowHiddenFiles is true, all hidden files including .settings are shown.
        /// </summary>
        private static bool ShouldHideInListing(string name, bool showHiddenFiles)
        {
            if (showHiddenFiles) return false; // Show everything when toggle is on
            return IsHiddenName(name);
        }

        private static Guid CreateDeterministicGuid(string value)
        {
            var data = Encoding.UTF8.GetBytes(value);
            var hash = MD5.HashData(data);
            return new Guid(hash);
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

        private async Task<bool> IsShowHiddenFilesEnabledAsync(string userId, CancellationToken cancellationToken)
        {
            var normalizedUserId = string.IsNullOrWhiteSpace(userId) ? "user" : userId.Trim();
            var feature = await _dbContext.UserFeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == normalizedUserId && x.Feature == FeatureName.ShowHiddenFiles, cancellationToken);

            return feature?.IsEnabled ?? false;
        }

        private async Task RemoveLegacySampleFilesAsync(CancellationToken cancellationToken)
        {
            var sampleFiles = await _dbContext.FileItems
                .Where(x => LegacySamplePaths.Contains(x.Path))
                .ToListAsync(cancellationToken);

            if (sampleFiles.Count == 0)
            {
                return;
            }

            var sampleIds = sampleFiles.Select(x => x.Id).ToList();
            var legacyAcls = await _dbContext.Acls
                .Where(x => sampleIds.Contains(x.FileItemId))
                .ToListAsync(cancellationToken);

            _dbContext.Acls.RemoveRange(legacyAcls);
            _dbContext.FileItems.RemoveRange(sampleFiles);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}


