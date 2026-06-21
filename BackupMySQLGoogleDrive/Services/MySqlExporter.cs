using MySql.Data.MySqlClient;

namespace BackupMySQLGoogleDrive.Services;

/// <summary>
/// Real <see cref="IMySqlExporter"/> backed by MySqlBackup.NET. Opens a connection
/// and exports the full schema + data to a <c>.sql</c> file.
/// </summary>
public sealed class MySqlExporter : IMySqlExporter
{
    public void ExportToFile(string connectionString, string filePath)
    {
        using var connection = new MySqlConnection(connectionString);
        using var command = new MySqlCommand { Connection = connection };
        using var backup = new MySqlBackup(command);

        connection.Open();
        backup.ExportToFile(filePath);
    }
}
