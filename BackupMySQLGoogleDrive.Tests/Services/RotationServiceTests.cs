using BackupMySQLGoogleDrive.Config;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests.Services;

public class RotationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);

    private static RotationService Create(FakeDrive drive, RotationSettings rotation)
    {
        var options = new BackupOptions
        {
            Backup = { FileNamePrefix = "backup" },
            Rotation = rotation,
        };
        var clock = new FixedTimeProvider(Now);
        return new RotationService(drive, Options.Create(options), clock, NullLogger<RotationService>.Instance);
    }

    private static DriveFileInfo Backup(string id, int daysOld) =>
        new(id, $"backup_{id}.sql.gz", Now.AddDays(-daysOld));

    [Fact]
    public async Task Apply_NoPolicies_DeletesNothing()
    {
        var drive = new FakeDrive(Backup("a", 1), Backup("b", 10), Backup("c", 100));

        await Create(drive, new RotationSettings()).ApplyAsync();

        Assert.Empty(drive.Deleted);
    }

    [Fact]
    public async Task Apply_KeepLastN_DeletesOldestBeyondN()
    {
        var drive = new FakeDrive(Backup("old", 30), Backup("mid", 10), Backup("new", 1));

        await Create(drive, new RotationSettings { KeepLastN = 2 }).ApplyAsync();

        Assert.Equal(new[] { "old" }, drive.Deleted);
    }

    [Fact]
    public async Task Apply_MaxAgeDays_DeletesFilesOlderThanCutoff()
    {
        var drive = new FakeDrive(Backup("ancient", 40), Backup("stale", 8), Backup("fresh", 2));

        await Create(drive, new RotationSettings { MaxAgeDays = 7 }).ApplyAsync();

        Assert.Equal(new[] { "ancient", "stale" }, drive.Deleted.OrderBy(x => x));
    }

    [Fact]
    public async Task Apply_BothPolicies_DeletesUnion()
    {
        // KeepLastN=2 would delete {old30, old20}; MaxAgeDays=7 would delete {old30, old20, old8}.
        var drive = new FakeDrive(
            Backup("old30", 30), Backup("old20", 20), Backup("old8", 8), Backup("new1", 1));

        await Create(drive, new RotationSettings { KeepLastN = 2, MaxAgeDays = 7 }).ApplyAsync();

        Assert.Equal(new[] { "old20", "old30", "old8" }, drive.Deleted.OrderBy(x => x));
    }

    [Fact]
    public async Task Apply_KeepLastN_NoDeletionWhenFewerThanN()
    {
        var drive = new FakeDrive(Backup("a", 5), Backup("b", 1));

        await Create(drive, new RotationSettings { KeepLastN = 5 }).ApplyAsync();

        Assert.Empty(drive.Deleted);
    }

    private sealed class FakeDrive : IGoogleDriveService
    {
        private readonly List<DriveFileInfo> _files;
        public List<string> Deleted { get; } = new();

        public FakeDrive(params DriveFileInfo[] files) => _files = files.ToList();

        public Task<string> UploadAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DriveFileInfo>> ListBackupsAsync(string prefix, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DriveFileInfo> ordered = _files.OrderBy(f => f.CreatedTime).ToList();
            return Task.FromResult(ordered);
        }

        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(fileId);
            return Task.CompletedTask;
        }
    }
}
