using OpensourceLab.FileStorage.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpensourceLab.FileStorage.ServedServices
{
    public interface IServeFiles
    {
        Task<IEnumerable<FileItem>> GetFilesAsync(string parentPath, CancellationToken cancellationToken);
        Task<IEnumerable<FileCardDto>> GetFileCardsAsync(string parentPath, string userId, CancellationToken cancellationToken);
        Task UpsertFilePermissionsAsync(FilePermissionUpsertRequest request, CancellationToken cancellationToken);
        Task PostProcessUploadedFile(FileItem fileItem, CancellationToken cancellationToken);
        Task DeleteFileByVirtualPathAsync(string virtualPath, string userId, CancellationToken cancellationToken);
        Task DeleteFolderByVirtualPathAsync(string virtualFolderPath, string userId, CancellationToken cancellationToken);
    }
}
