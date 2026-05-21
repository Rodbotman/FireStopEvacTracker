namespace FireStopEvacTracker.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string PostmarkServerToken { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "FireStop Evac Tracker";
    public string AppBaseUrl { get; set; } = string.Empty;
}
