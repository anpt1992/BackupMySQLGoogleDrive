namespace BackupMySQLGoogleDrive.Services;

/// <summary>
/// Thrown when the MySQL dump cannot be produced (e.g. the server is unreachable
/// or the credentials are wrong).
/// </summary>
public sealed class MySqlDumpException : Exception
{
    public MySqlDumpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
