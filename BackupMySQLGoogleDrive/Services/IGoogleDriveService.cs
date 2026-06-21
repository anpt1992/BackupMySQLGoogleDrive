namespace BackupMySQLGoogleDrive.Services;

/// <summary>
/// A backup file that lives in the target Drive folder.
/// </summary>
public sealed record DriveFileInfo(string Id, string Name, DateTimeOffset CreatedTime);

public interface IGoogleDriveService
{
    /// <summary>Uploads a local file into the configured Drive folder and returns its file id.</summary>
    Task<string> UploadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Lists backup files in the configured folder whose name starts with the given prefix, oldest first.</summary>
    Task<IReadOnlyList<DriveFileInfo>> ListBackupsAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file from Drive by id.</summary>
    Task DeleteAsync(string fileId, CancellationToken cancellationToken = default);
}
