using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.Meta;
using OpensourceLab.FileStorage.ServedServices;
using System.Text.Json;
using Shed.Data;

namespace Shed.Services
{
    public class ServeUserPreferencesService : IServeUserPreferences
    {
        private const string SettingsFolderName = ".settings";
        private const string SettingsFileName = "ui-settings.json";
        private const string VirtualSettingsPrefix = "~/.settings/";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly VanillaDbContext _dbContext;
        private readonly string _storageRoot;
        private readonly string _publicOwnerId;

        public ServeUserPreferencesService(VanillaDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _storageRoot = configuration["Storage:RootPath"]
                ?? @"/app/data/prodstorage";
            _publicOwnerId = configuration["Storage:PublicOwnerId"]
                ?? Guid.Empty.ToString();
        }

        public async Task<IEnumerable<UserFeatureFlag>> GetUserPreferencesAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.UserFeatureFlags
                .AsNoTracking()
                .OrderBy(x => x.UserId)
                .ThenBy(x => x.Feature)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserSettingsDto> GetUserSettingsAsync(string userId, CancellationToken cancellationToken)
        {
            var normalizedUserId = NormalizeUser(userId);

            var publicDefaults = await ReadSettingsDocumentOrDefaultAsync(_publicOwnerId, cancellationToken);
            var userSettings = await ReadSettingsDocumentOrDefaultAsync(normalizedUserId, cancellationToken);
            var effectiveSettings = userSettings ?? publicDefaults ?? new StorageSettingsDocument();

            effectiveSettings.ThumbnailLogoUrl = NormalizeToVirtualSettingsPath(effectiveSettings.ThumbnailLogoUrl);

            await UpsertFeatureFlagInternalAsync(normalizedUserId, effectiveSettings.ShowHiddenFiles, cancellationToken);

            return new UserSettingsDto(
                normalizedUserId,
                effectiveSettings.ShowHiddenFiles,
                effectiveSettings.TranslatedText,
                effectiveSettings.ThumbnailLogoUrl,
                effectiveSettings.FontStyle,
                effectiveSettings.Theme,
                effectiveSettings.PrimaryColor,
                effectiveSettings.UpdatedAt);
        }

        public async Task UpsertUserSettingsAsync(UserSettingsUpsertRequest request, CancellationToken cancellationToken)
        {
            var normalizedUserId = NormalizeUser(request.UserId);
            var now = DateTime.UtcNow;

            await EnsureUserSettingsSeededFromFallbackAsync(normalizedUserId, cancellationToken);
            var existingDoc = await ReadSettingsDocumentOrDefaultAsync(normalizedUserId, cancellationToken);

            var updated = new StorageSettingsDocument
            {
                ShowHiddenFiles = request.ShowHiddenFiles,
                TranslatedText = request.TranslatedText,
                ThumbnailLogoUrl = NormalizeToVirtualSettingsPath(request.ThumbnailLogoUrl),
                FontStyle = request.FontStyle,
                Theme = request.Theme,
                PrimaryColor = request.PrimaryColor,
                CreatedAt = existingDoc?.CreatedAt ?? now,
                UpdatedAt = now
            };

            await WriteSettingsDocumentAsync(normalizedUserId, updated, cancellationToken);
            await UpsertFeatureFlagInternalAsync(normalizedUserId, request.ShowHiddenFiles, cancellationToken);
        }

        public async Task UpsertUserPreferenceAsync(UserFeatureFlag userFeatureFlag, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.UserFeatureFlags
                .FirstOrDefaultAsync(x => x.UserId == userFeatureFlag.UserId && x.Feature == userFeatureFlag.Feature, cancellationToken);

            if (existing is null)
            {
                await _dbContext.UserFeatureFlags.AddAsync(userFeatureFlag with { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }, cancellationToken);
            }
            else
            {
                var updated = existing with { IsEnabled = userFeatureFlag.IsEnabled, UpdatedAt = DateTime.UtcNow };
                _dbContext.Entry(existing).CurrentValues.SetValues(updated);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string NormalizeUser(string userId)
        {
            return string.IsNullOrWhiteSpace(userId) ? "user" : userId.Trim();
        }

        private async Task UpsertFeatureFlagInternalAsync(string userId, bool isEnabled, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var featureFlag = await _dbContext.UserFeatureFlags
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Feature == FeatureName.ShowHiddenFiles, cancellationToken);

            if (featureFlag is null)
            {
                await _dbContext.UserFeatureFlags.AddAsync(new UserFeatureFlag(userId, FeatureName.ShowHiddenFiles, isEnabled, now, now), cancellationToken);
            }
            else
            {
                var updated = featureFlag with { IsEnabled = isEnabled, UpdatedAt = now };
                _dbContext.Entry(featureFlag).CurrentValues.SetValues(updated);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private string GetSettingsFilePath(string userId)
        {
            var ownerFolder = SanitizeFolderSegment(userId);
            return Path.Combine(_storageRoot, ownerFolder, SettingsFolderName, SettingsFileName);
        }

        private string GetSettingsDirectoryPath(string userId)
        {
            var ownerFolder = SanitizeFolderSegment(userId);
            return Path.Combine(_storageRoot, ownerFolder, SettingsFolderName);
        }

        private async Task<StorageSettingsDocument?> ReadSettingsDocumentOrDefaultAsync(string userId, CancellationToken cancellationToken)
        {
            var filePath = GetSettingsFilePath(userId);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var document = JsonSerializer.Deserialize<StorageSettingsDocument>(json, JsonOptions);
            if (document is null)
            {
                return null;
            }

            document.ThumbnailLogoUrl = NormalizeToVirtualSettingsPath(document.ThumbnailLogoUrl);
            return document;
        }

        private async Task WriteSettingsDocumentAsync(string userId, StorageSettingsDocument document, CancellationToken cancellationToken)
        {
            var filePath = GetSettingsFilePath(userId);
            var directory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException("Invalid settings file path.");

            Directory.CreateDirectory(directory);
            var payload = JsonSerializer.Serialize(document, JsonOptions);
            await File.WriteAllTextAsync(filePath, payload, cancellationToken);
        }

        private async Task EnsureUserSettingsSeededFromFallbackAsync(string userId, CancellationToken cancellationToken)
        {
            var userSettingsFile = GetSettingsFilePath(userId);
            if (File.Exists(userSettingsFile))
            {
                return;
            }

            var userSettingsDirectory = GetSettingsDirectoryPath(userId);
            Directory.CreateDirectory(userSettingsDirectory);

            var fallbackDirectory = GetSettingsDirectoryPath(_publicOwnerId);
            if (Directory.Exists(fallbackDirectory))
            {
                foreach (var sourceFile in Directory.GetFiles(fallbackDirectory))
                {
                    var fileName = Path.GetFileName(sourceFile);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    var destinationFile = Path.Combine(userSettingsDirectory, fileName);
                    if (!File.Exists(destinationFile))
                    {
                        File.Copy(sourceFile, destinationFile);
                    }
                }
            }

            if (!File.Exists(userSettingsFile))
            {
                var fallbackDoc = await ReadSettingsDocumentOrDefaultAsync(_publicOwnerId, cancellationToken)
                    ?? new StorageSettingsDocument();

                fallbackDoc.CreatedAt = DateTime.UtcNow;
                fallbackDoc.UpdatedAt = DateTime.UtcNow;
                await WriteSettingsDocumentAsync(userId, fallbackDoc, cancellationToken);
            }
        }

        private string NormalizeToVirtualSettingsPath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return VirtualSettingsPrefix + "background.jpeg";
            }

            var normalized = rawPath.Trim().Replace('\\', '/');
            if (normalized.StartsWith("~/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith("/.settings/", StringComparison.OrdinalIgnoreCase))
            {
                return "~" + normalized;
            }

            if (normalized.StartsWith(".settings/", StringComparison.OrdinalIgnoreCase))
            {
                return "~/" + normalized;
            }

            if (normalized.StartsWith("/ProdStorage/", StringComparison.OrdinalIgnoreCase))
            {
                var settingsIndex = normalized.IndexOf("/.settings/", StringComparison.OrdinalIgnoreCase);
                if (settingsIndex >= 0)
                {
                    return "~" + normalized.Substring(settingsIndex);
                }
            }

            var rootAsForwardSlashes = _storageRoot.Replace('\\', '/');
            if (normalized.StartsWith(rootAsForwardSlashes, StringComparison.OrdinalIgnoreCase))
            {
                var settingsIndex = normalized.IndexOf("/.settings/", StringComparison.OrdinalIgnoreCase);
                if (settingsIndex >= 0)
                {
                    return "~" + normalized.Substring(settingsIndex);
                }
            }

            return normalized;
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

        private sealed class StorageSettingsDocument
        {
            public bool ShowHiddenFiles { get; set; }
            public string TranslatedText { get; set; } = "My translated label";
            public string ThumbnailLogoUrl { get; set; } = VirtualSettingsPrefix + "background.jpeg";
            public string FontStyle { get; set; } = "Ubuntu";
            public string Theme { get; set; } = "ubuntu";
            public string PrimaryColor { get; set; } = "#E95420";
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }
    }
}


