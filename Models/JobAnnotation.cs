using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class JobAnnotation
{
    public int Id { get; set; }

    [Required]
    public int JobApprovalId { get; set; }

    [Required]
    public string CanvasDataUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public JobApproval JobApproval { get; set; } = null!;
}
