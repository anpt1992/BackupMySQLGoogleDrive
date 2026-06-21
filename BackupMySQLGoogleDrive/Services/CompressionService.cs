using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace BackupMySQLGoogleDrive.Services;

public sealed class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;

    public CompressionService(ILogger<CompressionService> logger) => _logger = logger;

    public string GzipFile(string filePath)
    {
        var destination = filePath + ".gz";

        _logger.LogInformation("Compressing {Source} to {Destination}", filePath, destination);

        using (var source = File.OpenRead(filePath))
        using (var target = File.Create(destination))
        using (var gzip = new GZipStream(target, CompressionLevel.Optimal))
        {
            source.CopyTo(gzip);
        }

        File.Delete(filePath);
        _logger.LogInformation("Compression complete, removed raw dump {Source}", filePath);

        return destination;
    }
}
