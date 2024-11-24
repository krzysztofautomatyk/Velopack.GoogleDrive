using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Velopack.Sources;

namespace Velopack.GoogleDrive;

public class GoogleDriveUpdateSource : IUpdateSource, IDisposable
{
    private const int MAX_PAGE_SIZE = 1000;
    private const string NUPKG_EXTENSION = ".nupkg";
    private readonly string _folderId;
    private readonly string _apiKey;
    private readonly string _packageId;
    private readonly DriveService _driveService;
    private readonly Dictionary<string, string> _fileIdMap;
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Configuration options for GoogleDriveUpdateSource
    /// </summary>
    public class Options
    {
        /// <summary>
        /// The application name used for Google Drive API requests
        /// </summary>
        public string? ApplicationName { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the GoogleDriveUpdateSource class.
    /// </summary>
    /// <param name="folderPath">Google Drive folder ID or full URL to the folder</param>
    /// <param name="apiKey">Google Drive API key</param>
    /// <param name="packageId">The package identifier for the updates</param>
    /// <param name="logger">Optional logger for debugging and monitoring</param>
    /// <param name="options">Optional configuration options</param>
    /// <exception cref="ArgumentNullException">Thrown when folderPath, apiKey, or packageId is null</exception>
    /// <exception cref="ArgumentException">Thrown when folderPath is invalid</exception>
    public GoogleDriveUpdateSource(
        string folderPath,
        string apiKey,
        string packageId,
        ILogger? logger = null,
        Options? options = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentNullException(nameof(folderPath));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        _folderId = ExtractFolderId(folderPath);
        _apiKey = apiKey;
        _packageId = packageId;
        _logger = logger;
        _fileIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            ApiKey = _apiKey,
            ApplicationName = options?.ApplicationName ?? "VelopackUpdater"
        });
    }

    /// <summary>
    /// Extracts the folder ID from either a full Google Drive URL or a direct folder ID.
    /// </summary>
    /// <param name="folderPath">The folder path or URL</param>
    /// <returns>The extracted folder ID</returns>
    /// <exception cref="ArgumentException">Thrown when the folder ID cannot be extracted</exception>
    private static string ExtractFolderId(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be empty", nameof(folderPath));

        // Check if it's a full Google Drive URL
        if (folderPath.Contains("drive.google.com"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                folderPath,
                @"folders/([a-zA-Z0-9_-]+)");

            if (!match.Success)
                throw new ArgumentException("Invalid Google Drive URL format", nameof(folderPath));

            return match.Groups[1].Value;
        }

        // Assume it's a direct folder ID
        return folderPath;
    }

    /// <summary>
    /// Retrieves the release feed containing available updates.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations</param>
    /// <param name="channel">Update channel to check</param>
    /// <param name="stagingId">Optional staging ID for staged updates</param>
    /// <param name="latestLocalRelease">Information about the latest local release</param>
    /// <returns>A feed containing available updates</returns>
    public async Task<VelopackAssetFeed> GetReleaseFeed(
        ILogger logger,
        string channel,
        Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleDriveUpdateSource));

        try
        {
            var files = await ListNupkgFiles().ConfigureAwait(false);
            var assets = CreateAssetsFromFiles(files);
            return new VelopackAssetFeed { Assets = assets.ToArray() };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get release feed");
            throw;
        }
    }

    /// <summary>
    /// Lists all .nupkg files in the specified Google Drive folder.
    /// </summary>
    private async Task<IList<Google.Apis.Drive.v3.Data.File>> ListNupkgFiles()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleDriveUpdateSource));

        var query = BuildDriveQuery();
        var request = _driveService.Files.List();
        request.Q = query;
        request.Fields = "files(id, name, size)";
        request.PageSize = MAX_PAGE_SIZE;

        var result = await request.ExecuteAsync().ConfigureAwait(false);
        _logger?.LogInformation("Found {Count} .nupkg files in folder", result.Files.Count);
        return result.Files;
    }

    /// <summary>
    /// Builds the Google Drive query string for filtering files.
    /// </summary>
    private string BuildDriveQuery() =>
        $"'{_folderId}' in parents " +
        $"and mimeType!='application/vnd.google-apps.folder' " +
        $"and trashed=false " +
        $"and name contains '{NUPKG_EXTENSION}'";

    /// <summary>
    /// Creates VelopackAsset instances from Google Drive files.
    /// </summary>
    private List<VelopackAsset> CreateAssetsFromFiles(
        IList<Google.Apis.Drive.v3.Data.File> files)
    {
        var assets = new List<VelopackAsset>();

        foreach (var file in files)
        {
            if (!file.Name.EndsWith(NUPKG_EXTENSION, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var asset = CreateAssetFromFile(file);
                assets.Add(asset);
                _fileIdMap[asset.FileName] = file.Id;
                _logger?.LogDebug("Processed file: {FileName}", file.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to process file: {FileName}", file.Name);
            }
        }

        return assets;
    }

    /// <summary>
    /// Creates a single VelopackAsset from a Google Drive file.
    /// </summary>
    private VelopackAsset CreateAssetFromFile(Google.Apis.Drive.v3.Data.File file)
    {
        var version = ParseVersionFromFileName(file.Name);

        return new VelopackAsset
        {
            PackageId = _packageId,
            FileName = file.Name,
            Version = version,
            Size = file.Size ?? 0,
            SHA1 = null,
            Type = DetermineAssetType(file.Name)
        };
    }

    /// <summary>
    /// Determines the asset type based on the file name.
    /// </summary>
    private static VelopackAssetType DetermineAssetType(string fileName) =>
        fileName.Contains("-delta") ? VelopackAssetType.Delta : VelopackAssetType.Full;

    /// <summary>
    /// Downloads a specific release entry to a local file.
    /// </summary>
    /// <param name="logger">Logger instance for tracking the download</param>
    /// <param name="releaseEntry">The release entry to download</param>
    /// <param name="localFile">The local file path where the download will be saved</param>
    /// <param name="progress">Action to report download progress</param>
    /// <param name="cancelToken">Cancellation token for the download operation</param>
    public async Task DownloadReleaseEntry(
        ILogger logger,
        VelopackAsset releaseEntry,
        string localFile,
        Action<int> progress,
        CancellationToken cancelToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleDriveUpdateSource));

        if (!_fileIdMap.TryGetValue(releaseEntry.FileName, out var fileId))
        {
            throw new FileNotFoundException($"File ID not found for '{releaseEntry.FileName}'.");
        }

        await DownloadFile(fileId, localFile, progress, cancelToken).ConfigureAwait(false);
        releaseEntry.SHA1 = await CalculateFileSHA1(localFile).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from Google Drive.
    /// </summary>
    private async Task DownloadFile(
        string fileId,
        string localFile,
        Action<int>? progress,
        CancellationToken cancelToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleDriveUpdateSource));

        // Pobierz informacje o pliku
        var getRequest = _driveService.Files.Get(fileId);
        getRequest.Fields = "size";  // Ustawiamy Fields bezpośrednio jako właściwość
        var file = await getRequest.ExecuteAsync(cancelToken).ConfigureAwait(false);

        var mediaRequest = _driveService.Files.Get(fileId);
        AddDownloadProgressHandler(mediaRequest, file.Size ?? 0, progress);

        using var fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write);
        await mediaRequest.DownloadAsync(fileStream, cancelToken).ConfigureAwait(false);

        _logger?.LogInformation("Successfully downloaded file: {FileName}", Path.GetFileName(localFile));
    }

    /// <summary>
    /// Adds the progress tracking handler for file downloads.
    /// </summary>
    private void AddDownloadProgressHandler(
        FilesResource.GetRequest request,
        long fileSize,
        Action<int>? progress)
    {
        request.MediaDownloader.ProgressChanged += (progressValue) =>
        {
            switch (progressValue.Status)
            {
                case Google.Apis.Download.DownloadStatus.Downloading:
                    var percent = (int)((progressValue.BytesDownloaded * 100) / fileSize);
                    progress?.Invoke(percent);
                    _logger?.LogDebug("Download progress: {Percent}%", percent);
                    break;

                case Google.Apis.Download.DownloadStatus.Completed:
                    progress?.Invoke(100);
                    _logger?.LogInformation("Download completed");
                    break;

                case Google.Apis.Download.DownloadStatus.Failed:
                    var error = "Download failed";
                    _logger?.LogError(error);
                    throw new DownloadException(error);
            }
        };
    }

    /// <summary>
    /// Calculates the SHA1 hash of a file.
    /// </summary>
    private static async Task<string> CalculateFileSHA1(string filePath)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        var hash = await sha1.ComputeHashAsync(stream).ConfigureAwait(false);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }

    /// <summary>
    /// Extracts the semantic version from a file name.
    /// </summary>
    private static SemanticVersion ParseVersionFromFileName(string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        const string versionPattern = @"(\d+\.\d+\.\d+(?:\.\d+)?)";

        var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExtension, versionPattern);

        if (match.Success && SemanticVersion.TryParse(match.Value, out var version))
        {
            return version;
        }
        else
        {
            // Możesz rzucić bardziej szczegółowy wyjątek lub zwrócić wartość domyślną
            throw new FormatException($"Cannot extract version from file name '{fileName}'.");
        }
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _driveService?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Exception thrown when a download operation fails.
/// </summary>
public class DownloadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the DownloadException class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    public DownloadException(string message) : base(message) { }
}
