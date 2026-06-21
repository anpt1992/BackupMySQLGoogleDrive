using System.Net.Http.Json;
using BackupMySQLGoogleDrive.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupMySQLGoogleDrive.Services;

public sealed class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly BackupOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        HttpClient httpClient,
        IOptions<BackupOptions> options,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(bool success, string summary, CancellationToken cancellationToken = default)
    {
        var notifications = _options.Notifications;

        if (success && !notifications.NotifyOnSuccess)
        {
            _logger.LogDebug("Skipping success notification (NotifyOnSuccess is disabled).");
            return;
        }

        if (string.IsNullOrWhiteSpace(notifications.WebhookUrl))
        {
            _logger.LogDebug("No webhook URL configured; skipping notification.");
            return;
        }

        var status = success ? "✅ Backup succeeded" : "❌ Backup failed";
        var message = $"{status}\n{summary}";

        // Shape the payload for both Discord (`content`) and Slack (`text`) webhooks.
        var payload = new { content = message, text = message };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                notifications.WebhookUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Notification webhook posted ({Status}).", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            // A failed webhook must never crash the backup run.
            _logger.LogWarning(ex, "Failed to post notification webhook.");
        }
    }
}
