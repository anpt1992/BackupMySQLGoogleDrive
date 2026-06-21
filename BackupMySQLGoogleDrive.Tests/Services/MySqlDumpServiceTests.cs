using BackupMySQLGoogleDrive.Config;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests.Services;

public class MySqlDumpServiceTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "BackupMySQLGoogleDriveTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private MySqlDumpService CreateService(IMySqlExporter exporter, BackupOptions? options = null)
    {
        options ??= new BackupOptions
        {
            MySql = { ConnectionString = "server=localhost;database=phr;", DatabaseName = "phr" },
            Backup = { TempDirectory = _tempDir, FileNamePrefix = "backup" },
        };

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 21, 14, 5, 0, TimeSpan.Zero));
        return new MySqlDumpService(Options.Create(options), exporter, clock, NullLogger<MySqlDumpService>.Instance);
    }

    [Fact]
    public void Dump_ReturnsTimestampedPathInTempDirectory()
    {
        var exporter = new FakeExporter();
        var service = CreateService(exporter);

        var path = service.Dump();

        Assert.Equal(Path.Combine(_tempDir, "backup_2026-06-21_1405.sql"), path);
    }

    [Fact]
    public void Dump_CreatesTempDirectoryWhenMissing()
    {
        var exporter = new FakeExporter();
        var service = CreateService(exporter);

        service.Dump();

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Dump_PassesConnectionStringAndTargetPathToExporter()
    {
        var exporter = new FakeExporter();
        var service = CreateService(exporter);

        var path = service.Dump();

        Assert.Equal("server=localhost;database=phr;", exporter.ConnectionString);
        Assert.Equal(path, exporter.FilePath);
    }

    [Fact]
    public void Dump_WrapsExporterFailureWithClearError()
    {
        var exporter = new FakeExporter { Throw = new InvalidOperationException("connect timeout") };
        var service = CreateService(exporter);

        var ex = Assert.Throws<MySqlDumpException>(() => service.Dump());

        Assert.Contains("phr", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private sealed class FakeExporter : IMySqlExporter
    {
        public string? ConnectionString { get; private set; }
        public string? FilePath { get; private set; }
        public Exception? Throw { get; init; }

        public void ExportToFile(string connectionString, string filePath)
        {
            ConnectionString = connectionString;
            FilePath = filePath;
            if (Throw is not null)
            {
                throw Throw;
            }
        }
    }
}
