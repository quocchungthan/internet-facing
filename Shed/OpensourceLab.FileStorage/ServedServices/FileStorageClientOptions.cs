namespace OpensourceLab.FileStorage.ServedServices
{
    /// <summary>
    /// Configuration for a .NET HTTP client that calls the FileStorage API using an API key.
    /// </summary>
    public class FileStorageClientOptions
    {
        /// <summary>Base URL of the FileStorage server, e.g. "https://files.example.com"</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// API key issued by the server. Send as the <c>X-Access-Key</c> header on every request.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
