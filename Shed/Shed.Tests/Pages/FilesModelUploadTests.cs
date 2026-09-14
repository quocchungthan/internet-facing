using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shed.Pages;
using Xunit;

namespace Shed.Tests.Pages;

public sealed class FilesModelUploadTests
{
    [Fact]
    public async Task OnPostUploadAsync_RedirectsBackToCurrentParent_WhenFileListIsNull()
    {
        var model = CreateModel();
        model.ParentPath = "/docs";

        var result = await model.OnPostUploadAsync(uploadFiles: null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/docs", redirect.RouteValues?["parentPath"]);
    }

    [Fact]
    public async Task OnPostUploadAsync_RedirectsBackToCurrentParent_WhenAllFilesAreEmpty()
    {
        var model = CreateModel();
        model.ParentPath = "/docs";
        var files = new List<IFormFile>
        {
            new FormFile(Stream.Null, 0, 0, "UploadFiles", "empty.txt")
        };

        var result = await model.OnPostUploadAsync(files, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/docs", redirect.RouteValues?["parentPath"]);
    }

    private static FilesModel CreateModel()
    {
        return new FilesModel(
            serveFiles: null!,
            configuration: new ConfigurationBuilder().Build(),
            vanillaDbContext: null!,
            storageOwnerResolver: null!);
    }
}
