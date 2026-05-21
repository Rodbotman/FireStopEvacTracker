using Microsoft.Extensions.Options;
using PostmarkDotNet;

namespace FireStopEvacTracker.Services;

public class PostmarkEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<PostmarkEmailService> _logger;

    public PostmarkEmailService(IOptions<EmailOptions> options, ILogger<PostmarkEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(string toAddress, string subject, string htmlBody, string? textBody = null, string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(_options.PostmarkServerToken))
        {
            _logger.LogError("Postmark server token not configured");
            return new EmailSendResult(false, null, "Email service not configured");
        }

        var client = new PostmarkClient(_options.PostmarkServerToken);
        var message = new PostmarkMessage
        {
            From = string.IsNullOrWhiteSpace(_options.FromName)
                ? _options.FromAddress
                : $"{_options.FromName} <{_options.FromAddress}>",
            To = toAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            Tag = tag,
            TrackOpens = true,
            MessageStream = "outbound"
        };

        try
        {
            var response = await client.SendMessageAsync(message);
            if (response.Status == PostmarkStatus.Success)
            {
                _logger.LogInformation("Postmark sent {MessageId} to {To}", response.MessageID, toAddress);
                return new EmailSendResult(true, response.MessageID.ToString(), null);
            }

            _logger.LogWarning("Postmark send failed: {ErrorCode} {Message}", response.ErrorCode, response.Message);
            return new EmailSendResult(false, null, $"{response.ErrorCode}: {response.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postmark send threw");
            return new EmailSendResult(false, null, ex.Message);
        }
    }
}
