using BackupMySQLGoogleDrive.Config;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupMySQLGoogleDrive;

/// <summary>
/// Orchestrates a single backup run: dump → compress → upload → rotate → notify.
/// Returns a process exit code (0 = success, non-zero = failure) and always cleans up
/// local temp files.
/// </summary>
public sealed class BackupRunner
{
    private readonly IMySqlDumpService _dump;
    private readonly ICompressionService _compression;
    private readonly IGoogleDriveService _drive;
    private readonly IRotationService _rotation;
    private readonly INotificationService _notification;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupRunner> _logger;

    public BackupRunner(
        IMySqlDumpService dump,
        ICompressionService compression,
        IGoogleDriveService drive,
        IRotationService rotation,
        INotificationService notification,
        IOptions<BackupOptions> options,
        ILogger<BackupRunner> logger)
    {
        _dump = dump;
        _compression = compression;
        _drive = drive;
        _rotation = rotation;
        _notification = notification;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var tempFiles = new List<string>();
        try
        {
            _logger.LogInformation("Backup run started.");

            var dumpPath = _dump.Dump();
            tempFiles.Add(dumpPath);

            var uploadPath = dumpPath;
            if (_options.Backup.Compress)
            {
                uploadPath = _compression.GzipFile(dumpPath);
                tempFiles.Add(uploadPath);
            }

            var fileId = await _drive.UploadAsync(uploadPath, cancellationToken);
            _logger.LogInformation("Uploaded {File} to Drive (id {Id}).", Path.GetFileName(uploadPath), fileId);

            await _rotation.ApplyAsync(cancellationToken);

            await _notification.SendAsync(
                success: true,
                $"Uploaded {Path.GetFileName(uploadPath)} to Google Drive.",
                cancellationToken);

            _logger.LogInformation("Backup run completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup run failed.");
            await _notification.SendAsync(success: false, ex.Message, cancellationToken);
            return 1;
        }
        finally
        {
            CleanupTempFiles(tempFiles);
        }
    }

    private void CleanupTempFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogDebug("Removed temp file {Path}.", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove temp file {Path}.", path);
            }
        }
    }
}
