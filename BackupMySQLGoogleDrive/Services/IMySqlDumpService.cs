namespace BackupMySQLGoogleDrive.Services;

public interface IMySqlDumpService
{
    /// <summary>
    /// Dumps the configured MySQL database to a timestamped <c>.sql</c> file in the
    /// temp directory and returns the full path to that file.
    /// </summary>
    string Dump();
}
