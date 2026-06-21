using BackupMySQLGoogleDrive.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

var options = builder.Configuration.Get<BackupOptions>() ?? new BackupOptions();
Validate(options);

builder.Services.AddSingleton<IOptions<BackupOptions>>(Options.Create(options));

using var host = builder.Build();
await host.StartAsync();
await host.StopAsync();

return 0;

static void Validate(BackupOptions options)
{
    options.MySql.ConnectionString = Require(options.MySql.ConnectionString, "MySql:ConnectionString");
    options.MySql.DatabaseName = Require(options.MySql.DatabaseName, "MySql:DatabaseName");
    options.GoogleDrive.ClientId = Require(options.GoogleDrive.ClientId, "GoogleDrive:ClientId");
    options.GoogleDrive.ClientSecret = Require(options.GoogleDrive.ClientSecret, "GoogleDrive:ClientSecret");
    options.GoogleDrive.FolderId = Require(options.GoogleDrive.FolderId, "GoogleDrive:FolderId");
    options.GoogleDrive.ApplicationName = Require(options.GoogleDrive.ApplicationName, "GoogleDrive:ApplicationName");
    options.Backup.TempDirectory = Require(options.Backup.TempDirectory, "Backup:TempDirectory");
    options.Backup.FileNamePrefix = Require(options.Backup.FileNamePrefix, "Backup:FileNamePrefix");

    if (options.Rotation.KeepLastN is < 0)
    {
        throw new InvalidOperationException("Configuration value 'Rotation:KeepLastN' cannot be negative.");
    }

    if (options.Rotation.MaxAgeDays is < 0)
    {
        throw new InvalidOperationException("Configuration value 'Rotation:MaxAgeDays' cannot be negative.");
    }
}

static string Require(string? value, string path)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required configuration value: '{path}'.");
    }

    return value.Trim();
}
