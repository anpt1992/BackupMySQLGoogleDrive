using BackupMySQLGoogleDrive.Config;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests.Config;

public class BackupOptionsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new BackupOptions();

        Assert.True(options.Backup.Compress);
        Assert.Equal("backup", options.Backup.FileNamePrefix);
        Assert.True(options.GoogleDrive.SupportsSharedDrives);
        Assert.False(options.Notifications.NotifyOnSuccess);
    }
}
