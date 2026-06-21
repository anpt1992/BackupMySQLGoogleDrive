using BackupMySQLGoogleDrive.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupMySQLGoogleDrive.Services;

public sealed class MySqlDumpService : IMySqlDumpService
{
    private readonly BackupOptions _options;
    private readonly IMySqlExporter _exporter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MySqlDumpService> _logger;

    public MySqlDumpService(
        IOptions<BackupOptions> options,
        IMySqlExporter exporter,
        TimeProvider timeProvider,
        ILogger<MySqlDumpService> logger)
    {
        _options = options.Value;
        _exporter = exporter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string Dump()
    {
        Directory.CreateDirectory(_options.Backup.TempDirectory);

        var timestamp = _timeProvider.GetLocalNow().ToString("yyyy-MM-dd_HHmm");
        var fileName = $"{_options.Backup.FileNamePrefix}_{timestamp}.sql";
        var path = Path.Combine(_options.Backup.TempDirectory, fileName);

        _logger.LogInformation(
            "Dumping MySQL database {Database} to {Path}", _options.MySql.DatabaseName, path);

        try
        {
            _exporter.ExportToFile(_options.MySql.ConnectionString, path);
        }
        catch (Exception ex)
        {
            throw new MySqlDumpException(
                $"Failed to dump MySQL database '{_options.MySql.DatabaseName}'. " +
                "Verify the connection string and that the server is reachable.",
                ex);
        }

        _logger.LogInformation("MySQL dump complete: {Path}", path);
        return path;
    }
}
