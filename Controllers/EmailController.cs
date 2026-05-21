using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace FireStopEvacTracker.Controllers;

[ApiController]
[Route("api/email")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthService _authService;

    public EmailController(IEmailService emailService, IHttpContextAccessor httpContextAccessor, AuthService authService)
    {
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _authService = authService;
    }

    [HttpPost("test")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendTest([FromBody] EmailTestRequest request)
    {
        if (!await _authService.IsUserAuthorizedAsync(_httpContextAccessor.HttpContext!, UserRole.Admin))
            return Unauthorized(new { error = "Admins only" });

        if (string.IsNullOrWhiteSpace(request?.To))
            return BadRequest(new { error = "Missing 'to' address" });

        var sender = _httpContextAccessor.HttpContext?.Session.GetString("FullName") ?? "an admin";
        var subject = "FireStop Evac Tracker - test email";
        var html = $@"<p>Hello,</p>
<p>This is a test email triggered by <strong>{System.Net.WebUtility.HtmlEncode(sender)}</strong> from the FireStop Evac Tracker.</p>
<p>If you received this, Postmark integration is working.</p>
<p>Time of send: {DateTime.Now:dddd, MMMM d, yyyy h:mm tt} (server local)</p>";
        var text = $"FireStop Evac Tracker test email triggered by {sender}.\nServer time: {DateTime.Now:yyyy-MM-dd HH:mm}";

        var result = await _emailService.SendAsync(request.To, subject, html, text, tag: "test");
        if (result.Success)
            return Ok(new { messageId = result.MessageId });

        return StatusCode(500, new { error = result.ErrorMessage });
    }
}

public class EmailTestRequest
{
    public string? To { get; set; }
}
