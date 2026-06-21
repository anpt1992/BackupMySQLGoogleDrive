using System.IO.Compression;
using System.Text;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests.Services;

public class CompressionServiceTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "BackupMySQLGoogleDriveTests", Guid.NewGuid().ToString("N"));

    public CompressionServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteSampleFile(string content = "SELECT 1; -- some sql content to compress")
    {
        var path = Path.Combine(_tempDir, "backup_2026-06-21_1405.sql");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static CompressionService Create() => new(NullLogger<CompressionService>.Instance);

    [Fact]
    public void GzipFile_ReturnsPathWithGzExtension()
    {
        var source = WriteSampleFile();

        var result = Create().GzipFile(source);

        Assert.Equal(source + ".gz", result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void GzipFile_ProducesRecoverableContent()
    {
        const string content = "INSERT INTO t VALUES (1),(2),(3); -- payload";
        var source = WriteSampleFile(content);

        var result = Create().GzipFile(source);

        using var fs = File.OpenRead(result);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        Assert.Equal(content, reader.ReadToEnd());
    }

    [Fact]
    public void GzipFile_DeletesRawSourceAfterCompression()
    {
        var source = WriteSampleFile();

        Create().GzipFile(source);

        Assert.False(File.Exists(source));
    }
}
