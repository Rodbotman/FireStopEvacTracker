using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class JobDocument
{
    public int Id { get; set; }

    public int EvacJobId { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string DocumentPath { get; set; } = string.Empty;

    [Required]
    public string DocumentType { get; set; } = string.Empty; // e.g., "Invoice", "Specification", "Approval Form"

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string? UploadedBy { get; set; }

    // Navigation property
    public EvacJob? EvacJob { get; set; }
}
