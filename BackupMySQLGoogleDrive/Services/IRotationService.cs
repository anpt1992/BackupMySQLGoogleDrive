namespace BackupMySQLGoogleDrive.Services;

public interface IRotationService
{
    /// <summary>Enforces the configured retention policy on the Drive folder.</summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);
}
