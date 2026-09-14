using Microsoft.Extensions.Configuration;
using Shed.Services;
using Xunit;

namespace Shed.Tests.Services;

public sealed class LoginBackgroundResolverTests
{
    [Fact]
    public void ResolveBackgroundImageUrl_UsesThumbnailLogoUrlFromPublicSettings_WhenValidAndExisting()
    {
        using var sandbox = new TempStorageSandbox();
        var publicOwnerId = Guid.Empty.ToString();
        sandbox.WriteFile(publicOwnerId, ".settings/custom-bg.jpeg", "image");
        sandbox.WriteFile(
            publicOwnerId,
            ".settings/ui-settings.json",
            """
            {
              "thumbnailLogoUrl": "~/.settings/custom-bg.jpeg"
            }
            """);

        var resolver = CreateResolver(sandbox.StorageRoot, publicOwnerId);

        var url = resolver.ResolveBackgroundImageUrl();

        Assert.Equal($"/ProdStorage/{publicOwnerId}/.settings/custom-bg.jpeg", url);
    }

    [Fact]
    public void ResolveBackgroundImageUrl_FallsBackToLegacyPath_WhenSettingsMissing()
    {
        using var sandbox = new TempStorageSandbox();
        var publicOwnerId = Guid.Empty.ToString();

        var resolver = CreateResolver(sandbox.StorageRoot, publicOwnerId);

        var url = resolver.ResolveBackgroundImageUrl();

        Assert.Equal($"/ProdStorage/{publicOwnerId}/.settings/background.jpeg", url);
    }

    [Fact]
    public void ResolveBackgroundImageUrl_FallsBackToLegacyPath_WhenSettingsInvalidOrImageMissing()
    {
        using var sandbox = new TempStorageSandbox();
        var publicOwnerId = Guid.Empty.ToString();

        sandbox.WriteFile(
            publicOwnerId,
            ".settings/ui-settings.json",
            """
            {
              "thumbnailLogoUrl": "~/.settings/missing-bg.jpeg"
            }
            """);

        var resolver = CreateResolver(sandbox.StorageRoot, publicOwnerId);
        var missingImageUrl = resolver.ResolveBackgroundImageUrl();
        Assert.Equal($"/ProdStorage/{publicOwnerId}/.settings/background.jpeg", missingImageUrl);

        sandbox.WriteFile(publicOwnerId, ".settings/ui-settings.json", "{not-valid-json");
        var invalidJsonUrl = resolver.ResolveBackgroundImageUrl();
        Assert.Equal($"/ProdStorage/{publicOwnerId}/.settings/background.jpeg", invalidJsonUrl);
    }

    [Fact]
    public void ResolveBackgroundImageUrl_UsesLegacyBackgroundPictureKey_WhenPresent()
    {
        using var sandbox = new TempStorageSandbox();
        var publicOwnerId = Guid.Empty.ToString();
        sandbox.WriteFile(publicOwnerId, ".settings/backgroundpicture.jpg", "image");
        sandbox.WriteFile(
            publicOwnerId,
            ".settings/ui-settings.json",
            """
            {
              "backgroundpicture": "~/.settings/backgroundpicture.jpg"
            }
            """);

        var resolver = CreateResolver(sandbox.StorageRoot, publicOwnerId);

        var url = resolver.ResolveBackgroundImageUrl();

        Assert.Equal($"/ProdStorage/{publicOwnerId}/.settings/backgroundpicture.jpg", url);
    }

    [Fact]
    public void ResolveBackgroundImageUrl_UsesEmptyIdOwnerFallback_WhenConfiguredOwnerMissing()
    {
        using var sandbox = new TempStorageSandbox();
        sandbox.WriteFile("empty-id", ".settings/backgroundpicture.png", "image");
        sandbox.WriteFile(
            "empty-id",
            ".settings/ui-settings.json",
            """
            {
              "backgroundPicture": ".settings/backgroundpicture.png"
            }
            """);

        var resolver = CreateResolver(sandbox.StorageRoot, "custom-public-owner");

        var url = resolver.ResolveBackgroundImageUrl();

        Assert.Equal("/ProdStorage/empty-id/.settings/backgroundpicture.png", url);
    }

    [Fact]
    public void ResolveBackgroundPhysicalPath_ReturnsExistingConfiguredImagePath()
    {
        using var sandbox = new TempStorageSandbox();
        var publicOwnerId = Guid.Empty.ToString();
        sandbox.WriteFile(publicOwnerId, ".settings/custom-bg.jpeg", "image");
        sandbox.WriteFile(
            publicOwnerId,
            ".settings/ui-settings.json",
            """
            {
              "thumbnailLogoUrl": "~/.settings/custom-bg.jpeg"
            }
            """);

        var resolver = CreateResolver(sandbox.StorageRoot, publicOwnerId);

        var physicalPath = resolver.ResolveBackgroundPhysicalPath();

        Assert.False(string.IsNullOrWhiteSpace(physicalPath));
        Assert.True(File.Exists(physicalPath));
        Assert.EndsWith("custom-bg.jpeg", physicalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveBackgroundPhysicalPath_ReturnsNull_WhenNoCandidateImageExists()
    {
        using var sandbox = new TempStorageSandbox();
        var resolver = CreateResolver(sandbox.StorageRoot, Guid.Empty.ToString());

        var physicalPath = resolver.ResolveBackgroundPhysicalPath();

        Assert.Null(physicalPath);
    }

    private static LoginBackgroundResolver CreateResolver(string storageRoot, string publicOwnerId)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:RootPath"] = storageRoot,
                ["Storage:PublicOwnerId"] = publicOwnerId
            })
            .Build();

        return new LoginBackgroundResolver(config);
    }

    private sealed class TempStorageSandbox : IDisposable
    {
        public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "ShedTests", Guid.NewGuid().ToString("N"));

        public TempStorageSandbox()
        {
            Directory.CreateDirectory(StorageRoot);
        }

        public void WriteFile(string ownerId, string relativePath, string content)
        {
            var safeRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(StorageRoot, ownerId, safeRelativePath);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
        }
    }
}

