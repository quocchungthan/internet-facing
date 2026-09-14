using Xunit;

namespace Shed.Tests.Pages;

public sealed class FilesMarkupUploadTests
{
    [Fact]
    public void FilesPage_UsesMultiFileInputNameForUploadHandler()
    {
        var markup = LoadFilesMarkup();

        Assert.Contains("<input id=\"UploadFile\" type=\"file\" name=\"UploadFiles\"", markup, StringComparison.Ordinal);
        Assert.Contains("multiple", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void FilesPage_AppendsAllSelectedFilesToExistingUploadRequest()
    {
        var markup = LoadFilesMarkup();

        Assert.Contains("for (const file of selectedFiles)", markup, StringComparison.Ordinal);
        Assert.Contains("formData.append(\"UploadFiles\", file);", markup, StringComparison.Ordinal);
        Assert.Contains("doUpload(files);", markup, StringComparison.Ordinal);
    }

    private static string LoadFilesMarkup()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Shed"));
        var markupPath = Path.Combine(projectRoot, "Pages", "Files.cshtml");
        return File.ReadAllText(markupPath);
    }
}
