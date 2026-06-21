namespace BackupMySQLGoogleDrive.Services;

public interface ICompressionService
{
    /// <summary>
    /// Gzips <paramref name="filePath"/> to <c>{filePath}.gz</c>, deletes the raw source on
    /// success, and returns the path to the compressed file.
    /// </summary>
    string GzipFile(string filePath);
}
