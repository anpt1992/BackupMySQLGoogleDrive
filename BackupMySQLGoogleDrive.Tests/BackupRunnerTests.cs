using BackupMySQLGoogleDrive;
using BackupMySQLGoogleDrive.Config;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests;

public class BackupRunnerTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "BackupMySQLGoogleDriveTests", Guid.NewGuid().ToString("N"));

    public BackupRunnerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string SqlPath => Path.Combine(_tempDir, "backup_2026-06-21_1405.sql");
    private string GzPath => SqlPath + ".gz";

    private BackupRunner Create(
        FakeDump dump, FakeCompression compression, FakeDrive drive, FakeRotation rotation,
        FakeNotification notification, bool compress = true)
    {
        var options = new BackupOptions { Backup = { Compress = compress } };
        return new BackupRunner(
            dump, compression, drive, rotation, notification,
            Options.Create(options), NullLogger<BackupRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_HappyPath_RunsEveryStepAndReturnsZero()
    {
        var dump = new FakeDump(SqlPath, createFile: true);
        var compression = new FakeCompression(GzPath, createFile: true);
        var drive = new FakeDrive();
        var rotation = new FakeRotation();
        var notification = new FakeNotification();

        var exit = await Create(dump, compression, drive, rotation, notification).RunAsync();

        Assert.Equal(0, exit);
        Assert.True(dump.Called);
        Assert.Equal(SqlPath, compression.CompressedInput);
        Assert.Equal(GzPath, drive.UploadedPath);
        Assert.True(rotation.Called);
        Assert.True(notification.Success);
    }

    [Fact]
    public async Task RunAsync_CompressDisabled_UploadsRawSql()
    {
        var dump = new FakeDump(SqlPath, createFile: true);
        var compression = new FakeCompression(GzPath, createFile: true);
        var drive = new FakeDrive();

        await Create(dump, compression, drive, new FakeRotation(), new FakeNotification(), compress: false)
            .RunAsync();

        Assert.Null(compression.CompressedInput);
        Assert.Equal(SqlPath, drive.UploadedPath);
    }

    [Fact]
    public async Task RunAsync_DeletesLocalTempFilesOnSuccess()
    {
        var dump = new FakeDump(SqlPath, createFile: true);
        var compression = new FakeCompression(GzPath, createFile: true, deleteInput: true);

        await Create(dump, compression, new FakeDrive(), new FakeRotation(), new FakeNotification()).RunAsync();

        Assert.False(File.Exists(SqlPath));
        Assert.False(File.Exists(GzPath));
    }

    [Fact]
    public async Task RunAsync_DumpFails_ReturnsNonZeroAndNotifiesFailure()
    {
        var dump = new FakeDump(SqlPath, createFile: false) { Throw = new InvalidOperationException("db down") };
        var notification = new FakeNotification();

        var exit = await Create(dump, new FakeCompression(GzPath, false), new FakeDrive(), new FakeRotation(), notification)
            .RunAsync();

        Assert.NotEqual(0, exit);
        Assert.False(notification.Success);
        Assert.Contains("db down", notification.Summary);
    }

    [Fact]
    public async Task RunAsync_UploadFails_CleansUpAndNotifiesFailure()
    {
        var dump = new FakeDump(SqlPath, createFile: true);
        var compression = new FakeCompression(GzPath, createFile: true, deleteInput: true);
        var drive = new FakeDrive { Throw = new InvalidOperationException("upload error") };
        var notification = new FakeNotification();

        var exit = await Create(dump, compression, drive, new FakeRotation(), notification).RunAsync();

        Assert.NotEqual(0, exit);
        Assert.False(notification.Success);
        Assert.False(File.Exists(GzPath));
    }

    private sealed class FakeDump : IMySqlDumpService
    {
        private readonly string _path;
        private readonly bool _createFile;
        public bool Called { get; private set; }
        public Exception? Throw { get; init; }

        public FakeDump(string path, bool createFile)
        {
            _path = path;
            _createFile = createFile;
        }

        public string Dump()
        {
            Called = true;
            if (Throw is not null)
            {
                throw Throw;
            }

            if (_createFile)
            {
                File.WriteAllText(_path, "sql");
            }

            return _path;
        }
    }

    private sealed class FakeCompression : ICompressionService
    {
        private readonly string _output;
        private readonly bool _createFile;
        private readonly bool _deleteInput;
        public string? CompressedInput { get; private set; }

        public FakeCompression(string output, bool createFile, bool deleteInput = false)
        {
            _output = output;
            _createFile = createFile;
            _deleteInput = deleteInput;
        }

        public string GzipFile(string filePath)
        {
            CompressedInput = filePath;
            if (_createFile)
            {
                File.WriteAllText(_output, "gz");
            }

            if (_deleteInput && File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return _output;
        }
    }

    private sealed class FakeDrive : IGoogleDriveService
    {
        public string? UploadedPath { get; private set; }
        public Exception? Throw { get; init; }

        public Task<string> UploadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            UploadedPath = filePath;
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult("file-id");
        }

        public Task<IReadOnlyList<DriveFileInfo>> ListBackupsAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DriveFileInfo>>(Array.Empty<DriveFileInfo>());

        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRotation : IRotationService
    {
        public bool Called { get; private set; }

        public Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotification : INotificationService
    {
        public bool? Success { get; private set; }
        public string? Summary { get; private set; }

        public Task SendAsync(bool success, string summary, CancellationToken cancellationToken = default)
        {
            Success = success;
            Summary = summary;
            return Task.CompletedTask;
        }
    }
}
