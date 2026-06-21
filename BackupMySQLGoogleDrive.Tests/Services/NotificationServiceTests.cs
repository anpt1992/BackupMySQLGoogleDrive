using System.Net;
using System.Text.Json;
using BackupMySQLGoogleDrive.Config;
using BackupMySQLGoogleDrive.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackupMySQLGoogleDrive.Tests.Services;

public class NotificationServiceTests
{
    private const string WebhookUrl = "https://hooks.example.com/abc";

    private static (NotificationService service, RecordingHandler handler) Create(
        NotificationSettings notifications, HttpStatusCode status = HttpStatusCode.NoContent)
    {
        var handler = new RecordingHandler(status);
        var client = new HttpClient(handler);
        var options = new BackupOptions { Notifications = notifications };
        var service = new NotificationService(client, Options.Create(options), NullLogger<NotificationService>.Instance);
        return (service, handler);
    }

    [Fact]
    public async Task Send_OnFailure_AlwaysPosts()
    {
        var (service, handler) = Create(new NotificationSettings { WebhookUrl = WebhookUrl, NotifyOnSuccess = false });

        await service.SendAsync(success: false, "it broke");

        Assert.NotNull(handler.LastRequestBody);
        var json = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.Contains("it broke", json.GetProperty("content").GetString());
        Assert.Contains("it broke", json.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_OnSuccess_WhenNotifyOnSuccessFalse_DoesNotPost()
    {
        var (service, handler) = Create(new NotificationSettings { WebhookUrl = WebhookUrl, NotifyOnSuccess = false });

        await service.SendAsync(success: true, "all good");

        Assert.Null(handler.LastRequestBody);
    }

    [Fact]
    public async Task Send_OnSuccess_WhenNotifyOnSuccessTrue_Posts()
    {
        var (service, handler) = Create(new NotificationSettings { WebhookUrl = WebhookUrl, NotifyOnSuccess = true });

        await service.SendAsync(success: true, "all good");

        Assert.NotNull(handler.LastRequestBody);
    }

    [Fact]
    public async Task Send_WhenWebhookUrlEmpty_DoesNotPost()
    {
        var (service, handler) = Create(new NotificationSettings { WebhookUrl = "", NotifyOnSuccess = false });

        await service.SendAsync(success: false, "it broke");

        Assert.Null(handler.LastRequestBody);
    }

    [Fact]
    public async Task Send_WhenWebhookThrows_DoesNotPropagate()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK) { ThrowOnSend = true };
        var client = new HttpClient(handler);
        var options = new BackupOptions
        {
            Notifications = new NotificationSettings { WebhookUrl = WebhookUrl, NotifyOnSuccess = false },
        };
        var service = new NotificationService(client, Options.Create(options), NullLogger<NotificationService>.Instance);

        // Should not throw.
        await service.SendAsync(success: false, "it broke");
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public bool ThrowOnSend { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new HttpRequestException("network down");
            }

            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
}
