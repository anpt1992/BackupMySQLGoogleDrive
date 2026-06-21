using BackupMySQLGoogleDrive.Config;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DriveData = Google.Apis.Drive.v3.Data;

namespace BackupMySQLGoogleDrive.Services;

public sealed class GoogleDriveService : IGoogleDriveService, IDisposable
{
    private const int MaxUploadAttempts = 3;
    private const string TokenStoreFolder = "MyAppsToken";

    private readonly GoogleDriveOptions _options;
    private readonly ILogger<GoogleDriveService> _logger;
    private DriveService? _service;

    public GoogleDriveService(IOptions<BackupOptions> options, ILogger<GoogleDriveService> logger)
    {
        _options = options.Value.GoogleDrive;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var stream = File.OpenRead(filePath);
                var body = new DriveData.File
                {
                    Name = Path.GetFileName(filePath),
                    Parents = new List<string> { _options.FolderId },
                };

                var request = service.Files.Create(body, stream, "application/octet-stream");
                request.SupportsAllDrives = _options.SupportsSharedDrives;
                request.Fields = "id";

                var progress = await request.UploadAsync(cancellationToken);

                if (progress.Status == UploadStatus.Completed && request.ResponseBody is not null)
                {
                    return request.ResponseBody.Id;
                }

                throw new InvalidOperationException(
                    $"Drive upload did not complete (status {progress.Status}).", progress.Exception);
            }
            catch (Exception ex) when (attempt < MaxUploadAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    ex, "Drive upload attempt {Attempt}/{Max} failed; retrying in {Delay}.",
                    attempt, MaxUploadAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<DriveFileInfo>> ListBackupsAsync(
        string prefix, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(cancellationToken);

        var results = new List<DriveFileInfo>();
        string? pageToken = null;

        do
        {
            var request = service.Files.List();
            request.Q = $"'{_options.FolderId}' in parents and name contains '{prefix}' and trashed = false";
            request.Fields = "nextPageToken, files(id, name, createdTime)";
            request.OrderBy = "createdTime";
            request.PageSize = 1000;
            request.PageToken = pageToken;

            if (_options.SupportsSharedDrives)
            {
                request.SupportsAllDrives = true;
                request.IncludeItemsFromAllDrives = true;
            }

            var response = await request.ExecuteAsync(cancellationToken);

            foreach (var file in response.Files ?? Enumerable.Empty<DriveData.File>())
            {
                // Drive's `name contains` is a loose substring match — keep only true prefix matches.
                if (file.Name is null || !file.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var created = file.CreatedTimeDateTimeOffset ?? DateTimeOffset.MinValue;
                results.Add(new DriveFileInfo(file.Id, file.Name, created));
            }

            pageToken = response.NextPageToken;
        }
        while (pageToken is not null);

        return results;
    }

    public async Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(cancellationToken);
        var request = service.Files.Delete(fileId);
        request.SupportsAllDrives = _options.SupportsSharedDrives;
        await request.ExecuteAsync(cancellationToken);
    }

    private async Task<DriveService> GetServiceAsync(CancellationToken cancellationToken)
    {
        if (_service is not null)
        {
            return _service;
        }

        var secrets = new ClientSecrets
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
        };

        // Interactive on first run; the refresh token is cached in TokenStoreFolder for scheduled runs.
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { DriveService.Scope.DriveFile },
            Environment.UserName,
            cancellationToken,
            new FileDataStore(TokenStoreFolder));

        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName,
        });

        // Large uploads can be slow; raise the default timeout.
        _service.HttpClient.Timeout = TimeSpan.FromMinutes(100);

        return _service;
    }

    public void Dispose() => _service?.Dispose();
}
