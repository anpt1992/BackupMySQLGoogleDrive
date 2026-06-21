namespace BackupMySQLGoogleDrive.Services;

public interface INotificationService
{
    /// <summary>
    /// Posts a notification to the configured webhook. Always sends on failure; sends on success
    /// only when <c>NotifyOnSuccess</c> is enabled. Never throws — webhook errors are logged.
    /// </summary>
    Task SendAsync(bool success, string summary, CancellationToken cancellationToken = default);
}
