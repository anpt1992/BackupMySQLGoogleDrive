namespace BackupMySQLGoogleDrive.Services;

/// <summary>
/// Abstracts the raw MySQL → file export so the dump orchestration can be unit-tested
/// without a live database.
/// </summary>
public interface IMySqlExporter
{
    void ExportToFile(string connectionString, string filePath);
}
