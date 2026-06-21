using BackupMySQLGoogleDrive.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupMySQLGoogleDrive.Services;

public sealed class RotationService : IRotationService
{
    private readonly IGoogleDriveService _drive;
    private readonly BackupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RotationService> _logger;

    public RotationService(
        IGoogleDriveService drive,
        IOptions<BackupOptions> options,
        TimeProvider timeProvider,
        ILogger<RotationService> logger)
    {
        _drive = drive;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var rotation = _options.Rotation;
        if (rotation.KeepLastN is null && rotation.MaxAgeDays is null)
        {
            _logger.LogInformation("No rotation policy configured; skipping retention.");
            return;
        }

        // Oldest first.
        var backups = await _drive.ListBackupsAsync(_options.Backup.FileNamePrefix, cancellationToken);

        var toDelete = new HashSet<DriveFileInfo>();

        if (rotation.KeepLastN is int keep && backups.Count > keep)
        {
            // Keep the newest N; everything before that is fair game.
            foreach (var file in backups.Take(backups.Count - keep))
            {
                toDelete.Add(file);
            }
        }

        if (rotation.MaxAgeDays is int maxAge)
        {
            var cutoff = _timeProvider.GetUtcNow().AddDays(-maxAge);
            foreach (var file in backups.Where(f => f.CreatedTime < cutoff))
            {
                toDelete.Add(file);
            }
        }

        if (toDelete.Count == 0)
        {
            _logger.LogInformation("Rotation: nothing to delete ({Count} backups retained).", backups.Count);
            return;
        }

        foreach (var file in toDelete)
        {
            _logger.LogInformation(
                "Rotation: deleting {Name} (id {Id}, created {Created:u}).", file.Name, file.Id, file.CreatedTime);
            await _drive.DeleteAsync(file.Id, cancellationToken);
        }

        _logger.LogInformation("Rotation complete: deleted {Deleted} of {Total} backups.", toDelete.Count, backups.Count);
    }
}
