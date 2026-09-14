using OpensourceLab.FileStorage.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpensourceLab.FileStorage.ServedServices
{
    /// <summary>
    /// Contract for a .NET HTTP client that consumes the FileStorage REST API.
    /// Implementations must send the configured API key as the <c>X-Access-Key</c> header.
    /// </summary>
    public interface IFileStorageClient
    {
        /// <summary>List direct-child files and folders at the given virtual parent path.</summary>
        Task<IEnumerable<FileCardDto>> GetFileCardsAsync(
            string parentPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upload a file into the given parent path.
        /// Returns the persisted <see cref="FileItem"/> metadata.
        /// </summary>
        Task<FileItem> UploadFileAsync(
            Stream content,
            string fileName,
            string contentType,
            string parentPath,
            CancellationToken cancellationToken = default);

        /// <summary>Download file content by its unique id.</summary>
        Task<Stream> DownloadFileAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Delete a single file by its virtual path.</summary>
        Task DeleteFileAsync(string virtualPath, CancellationToken cancellationToken = default);

        /// <summary>Delete a folder and all files under its virtual path.</summary>
        Task DeleteFolderAsync(string virtualFolderPath, CancellationToken cancellationToken = default);
    }
}
