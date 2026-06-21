namespace BackupMySQLGoogleDrive.Config;

public sealed class BackupOptions
{
    public MySqlOptions MySql { get; set; } = new();

    public GoogleDriveOptions GoogleDrive { get; set; } = new();

    public BackupSettings Backup { get; set; } = new();

    public RotationSettings Rotation { get; set; } = new();

    public NotificationSettings Notifications { get; set; } = new();
}

public sealed class MySqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;
}

public sealed class GoogleDriveOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;

    public bool SupportsSharedDrives { get; set; } = true;

    public string ApplicationName { get; set; } = "Backup MySQL To Google Drive";
}

public sealed class BackupSettings
{
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "BackupMySQLGoogleDrive");

    public string FileNamePrefix { get; set; } = "backup";

    public bool Compress { get; set; } = true;
}

public sealed class RotationSettings
{
    public int? KeepLastN { get; set; }

    public int? MaxAgeDays { get; set; }
}

public sealed class NotificationSettings
{
    public string WebhookUrl { get; set; } = string.Empty;

    public bool NotifyOnSuccess { get; set; }
}
