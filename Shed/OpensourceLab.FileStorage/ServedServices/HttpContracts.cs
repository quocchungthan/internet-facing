using System;
using System.Collections.Generic;

namespace OpensourceLab.FileStorage.ServedServices
{
    // ─────────────────────────────────────────────────────────────────────────
    // REST HTTP API contracts
    // These are the wire-level JSON shapes the server accepts and returns.
    // Implement IFileStorageClient using these types to consume the API.
    // ─────────────────────────────────────────────────────────────────────────

    // ─── Endpoints ────────────────────────────────────────────────────────────

    /// <summary>All REST endpoint paths exposed by the FileStorage server.</summary>
    public static class FileStorageEndpoints
    {
        /// <summary>GET  ?parentPath={path}</summary>
        public const string ListFiles = "/api/files";

        /// <summary>POST multipart/form-data  fields: file, parentPath</summary>
        public const string UploadFile = "/api/files/upload";

        /// <summary>GET  /{id}  — add ?download=true for attachment disposition</summary>
        public const string DownloadFile = "/file-content/{id}";

        /// <summary>DELETE  body: { virtualPath }</summary>
        public const string DeleteFile = "/api/files/delete";

        /// <summary>DELETE  body: { virtualFolderPath }</summary>
        public const string DeleteFolder = "/api/files/delete-folder";
    }

    // ─── Headers ──────────────────────────────────────────────────────────────

    /// <summary>Well-known HTTP headers used by the FileStorage API.</summary>
    public static class FileStorageHeaders
    {
        /// <summary>API key issued via the API Keys management page.</summary>
        public const string AccessKey = "X-Access-Key";
    }

    // ─── ListFiles ────────────────────────────────────────────────────────────

    /// <summary>Response body for <see cref="FileStorageEndpoints.ListFiles"/>.</summary>
    public class ListFilesResponse
    {
        public List<FileCardDto> Files { get; set; } = new();
    }

    // ─── UploadFile ───────────────────────────────────────────────────────────

    /// <summary>
    /// Response body for <see cref="FileStorageEndpoints.UploadFile"/>.
    /// The upload uses <c>multipart/form-data</c>; the response is JSON.
    /// </summary>
    public class UploadFileResponse
    {
        public string FileId     { get; set; } = string.Empty;
        public string Path       { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long   Size       { get; set; }
        public bool   Succeeded  { get; set; }
        public string Error      { get; set; } = string.Empty;
    }

    // ─── DeleteFile ───────────────────────────────────────────────────────────

    /// <summary>Request body for <see cref="FileStorageEndpoints.DeleteFile"/>.</summary>
    public class DeleteFileRequest
    {
        public string VirtualPath { get; set; } = string.Empty;
    }

    /// <summary>Response body for <see cref="FileStorageEndpoints.DeleteFile"/>.</summary>
    public class DeleteFileResponse
    {
        public bool   Succeeded { get; set; }
        public string Error     { get; set; } = string.Empty;
    }

    // ─── DeleteFolder ─────────────────────────────────────────────────────────

    /// <summary>Request body for <see cref="FileStorageEndpoints.DeleteFolder"/>.</summary>
    public class DeleteFolderRequest
    {
        public string VirtualFolderPath { get; set; } = string.Empty;
    }

    /// <summary>Response body for <see cref="FileStorageEndpoints.DeleteFolder"/>.</summary>
    public class DeleteFolderResponse
    {
        public bool   Succeeded     { get; set; }
        public int    FilesRemoved  { get; set; }
        public string Error         { get; set; } = string.Empty;
    }

    // ─── gRPC client options ──────────────────────────────────────────────────

    /// <summary>
    /// Configuration for a gRPC client that calls the FileStorage gRPC service.
    /// Complement to <see cref="FileStorageClientOptions"/> (REST).
    /// </summary>
    public class FileStorageGrpcClientOptions
    {
        /// <summary>gRPC server address, e.g. "https://files.example.com:5001"</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// API key sent as gRPC metadata key <c>x-access-key</c> on every call.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Maximum size in bytes for a single upload/download chunk.
        /// Default: 256 KB.
        /// </summary>
        public int ChunkSizeBytes { get; set; } = 256 * 1024;
    }

    // ─── gRPC IFileStorageGrpcClient ─────────────────────────────────────────

    /// <summary>
    /// Contract for a .NET gRPC client that consumes the FileStorage gRPC service.
    /// Generated Protobuf types live in <c>OpensourceLab.FileStorage.Grpc</c>.
    /// Implementations should use the generated <c>FileStorageService.FileStorageServiceClient</c>.
    /// </summary>
    public interface IFileStorageGrpcClient
    {
        /// <summary>List direct-child files and folders at the given virtual parent path.</summary>
        System.Threading.Tasks.Task<IEnumerable<FileCardDto>> ListFilesAsync(
            string parentPath,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Upload a file by streaming its content in chunks.
        /// Returns upload result including the server-assigned file ID.
        /// </summary>
        System.Threading.Tasks.Task<UploadFileResponse> UploadFileAsync(
            System.IO.Stream content,
            string fileName,
            string contentType,
            string parentPath,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>Download a file by its ID, streaming content back to the caller.</summary>
        System.Threading.Tasks.Task<System.IO.Stream> DownloadFileAsync(
            string fileId,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>Delete a single file by virtual path.</summary>
        System.Threading.Tasks.Task<DeleteFileResponse> DeleteFileAsync(
            string virtualPath,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>Delete a folder and all contents.</summary>
        System.Threading.Tasks.Task<DeleteFolderResponse> DeleteFolderAsync(
            string virtualFolderPath,
            System.Threading.CancellationToken cancellationToken = default);
    }
}
