namespace FireStopEvacTracker.Services;

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(string toAddress, string subject, string htmlBody, string? textBody = null, string? tag = null);
}

public record EmailSendResult(bool Success, string? MessageId, string? ErrorMessage);
