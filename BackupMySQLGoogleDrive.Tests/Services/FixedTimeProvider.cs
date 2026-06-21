namespace BackupMySQLGoogleDrive.Tests.Services;

/// <summary>A deterministic <see cref="TimeProvider"/> for tests, anchored to UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
