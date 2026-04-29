using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class EvacJob
{
    public int Id { get; set; }

    [Display(Name = "Date Started")]
    [DataType(DataType.Date)]
    public DateTime DateStarted { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Client Name")]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Site Address")]
    public string SiteAddress { get; set; } = string.Empty;

    [Display(Name = "Job Name")]
    public string JobName { get; set; } = string.Empty;

    public string Status { get; set; } = JobStatus.New;

    public string? Notes { get; set; }

    [Display(Name = "Draft PDF File Name")]
    public string? DraftPdfFileName { get; set; }

    [Display(Name = "Draft PDF Path")]
    public string? DraftPdfPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StatusUpdatedAt { get; set; }

    // Navigation property for notes
    public ICollection<JobNote> JobNotes { get; set; } = new List<JobNote>();

    public bool HasPdf => !string.IsNullOrWhiteSpace(DraftPdfPath);

    public string GetShortJobName()
    {
        if (string.IsNullOrWhiteSpace(JobName) || JobName.Length < 8)
            return JobName;

        // Extract date (first 8 chars) and remaining parts
        string datePrefix = JobName.Substring(0, 8);
        string remainder = JobName.Length > 8 ? JobName.Substring(9) : ""; // Skip the underscore

        if (string.IsNullOrEmpty(remainder))
            return datePrefix;

        // Split by underscore and take first 2-3 parts
        string[] parts = remainder.Split('_');
        int partCount = Math.Min(3, parts.Length); // Take up to 3 parts

        // Join first parts with spaces
        string mainParts = string.Join(" ", parts.Take(partCount));

        return $"{datePrefix} - {mainParts}";
    }
}

public static class JobStatus
{
    public const string New = "New";
    public const string Drafting = "Drafting";
    public const string SentToOffice = "Sent to Office";
    public const string SentToCustomer = "Sent to Customer";
    public const string ChangesNeeded = "Changes Needed";
    public const string Approved = "Approved";
    public const string Complete = "Complete";

    public static readonly string[] All =
    [
        New,
        Drafting,
        SentToOffice,
        SentToCustomer,
        ChangesNeeded,
        Approved,
        Complete
    ];
}
