using System.Text.Json;

namespace Shed.Services;

public interface ILoginBackgroundResolver
{
    string ResolveBackgroundImageUrl();
    string? ResolveBackgroundPhysicalPath();
}

public sealed class LoginBackgroundResolver : ILoginBackgroundResolver
{
    private const string SettingsFolderName = ".settings";
    private const string SettingsFileName = "ui-settings.json";
    private static readonly string[] LegacyBackgroundFileNames =
    [
        "background.jpeg",
        "background.jpg",
        "background.png",
        "backgroundpicture",
        "backgroundpicture.jpeg",
        "backgroundpicture.jpg",
        "backgroundpicture.png"
    ];

    private readonly string _storageRoot;
    private readonly string _publicOwnerId;

    public LoginBackgroundResolver(IConfiguration configuration)
    {
        _storageRoot = configuration["Storage:RootPath"]
            ?? @"/app/data/prodstorage";
        _publicOwnerId = configuration["Storage:PublicOwnerId"]
            ?? Guid.Empty.ToString();
    }

    public string ResolveBackgroundImageUrl()
    {
        var ownerCandidates = BuildOwnerCandidates();
        var legacyBackgroundUrl = BuildLegacyBackgroundUrl(ownerCandidates);
        var settingsContext = ownerCandidates
            .Select(ownerId => new
            {
                OwnerId = ownerId,
                Path = Path.Combine(_storageRoot, ownerId, SettingsFolderName, SettingsFileName)
            })
            .FirstOrDefault(x => File.Exists(x.Path));

        if (settingsContext is null)
        {
            return legacyBackgroundUrl;
        }

        try
        {
            var json = File.ReadAllText(settingsContext.Path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return legacyBackgroundUrl;
            }

            var rawBackgroundPath = ExtractBackgroundPath(json);
            var candidateUrl = NormalizeToPublicStorageUrl(rawBackgroundPath, settingsContext.OwnerId);
            if (string.IsNullOrWhiteSpace(candidateUrl))
            {
                return legacyBackgroundUrl;
            }

            if (TryResolveToPhysicalPath(candidateUrl, out var physicalPath) && !File.Exists(physicalPath))
            {
                return legacyBackgroundUrl;
            }

            return candidateUrl;
        }
        catch (JsonException)
        {
            return legacyBackgroundUrl;
        }
        catch (IOException)
        {
            return legacyBackgroundUrl;
        }
    }

    public string? ResolveBackgroundPhysicalPath()
    {
        var resolvedUrl = ResolveBackgroundImageUrl();
        if (TryResolveToPhysicalPath(resolvedUrl, out var resolvedPhysicalPath) && File.Exists(resolvedPhysicalPath))
        {
            return resolvedPhysicalPath;
        }

        var ownerCandidates = BuildOwnerCandidates();
        foreach (var ownerId in ownerCandidates)
        {
            foreach (var fileName in LegacyBackgroundFileNames)
            {
                var physicalPath = Path.Combine(_storageRoot, ownerId, SettingsFolderName, fileName);
                if (File.Exists(physicalPath))
                {
                    return physicalPath;
                }
            }
        }

        return null;
    }

    private List<string> BuildOwnerCandidates()
    {
        var candidates = new List<string>();

        void AddOwner(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = value.Trim();
            if (!candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(normalized);
            }
        }

        AddOwner(_publicOwnerId);
        AddOwner(Guid.Empty.ToString());
        AddOwner("empty-id");
        AddOwner("<empty-id>");

        if (candidates.Count == 0)
        {
            candidates.Add(Guid.Empty.ToString());
        }

        return candidates;
    }

    private string BuildLegacyBackgroundUrl(IEnumerable<string> ownerCandidates)
    {
        foreach (var ownerId in ownerCandidates)
        {
            foreach (var fileName in LegacyBackgroundFileNames)
            {
                var physicalPath = Path.Combine(_storageRoot, ownerId, SettingsFolderName, fileName);
                if (File.Exists(physicalPath))
                {
                    return $"/ProdStorage/{ownerId}/{SettingsFolderName}/{fileName}";
                }
            }
        }

        var fallbackOwner = ownerCandidates.FirstOrDefault() ?? Guid.Empty.ToString();
        return $"/ProdStorage/{fallbackOwner}/{SettingsFolderName}/background.jpeg";
    }

    private static string? ExtractBackgroundPath(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (TryGetString(root, out var value, "thumbnailLogoUrl", "backgroundpicture", "backgroundPicture", "backgroundImage", "background"))
        {
            return value;
        }

        if (root.TryGetProperty("login", out var loginNode)
            && loginNode.ValueKind == JsonValueKind.Object
            && TryGetString(loginNode, out value, "thumbnailLogoUrl", "backgroundpicture", "backgroundPicture", "backgroundImage", "background"))
        {
            return value;
        }

        return null;
    }

    private static bool TryGetString(JsonElement source, out string? value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (source.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = null;
        return false;
    }

    private string? NormalizeToPublicStorageUrl(string? rawPath, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var normalized = rawPath.Trim().Replace('\\', '/');
        if (normalized.StartsWith("~/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/ProdStorage/{ownerId}/{normalized[2..]}";
        }

        if (normalized.StartsWith("/.settings/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/ProdStorage/{ownerId}{normalized}";
        }

        if (normalized.StartsWith(".settings/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/ProdStorage/{ownerId}/{normalized}";
        }

        if (normalized.StartsWith("/ProdStorage/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return normalized;
    }

    private bool TryResolveToPhysicalPath(string candidateUrl, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (!candidateUrl.StartsWith("/ProdStorage/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = candidateUrl["/ProdStorage/".Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        physicalPath = Path.Combine(_storageRoot, relative);
        return true;
    }

    private sealed class StorageSettingsDocument
    {
        public string? ThumbnailLogoUrl { get; set; }
    }
}

