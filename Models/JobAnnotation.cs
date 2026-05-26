using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class JobAnnotation
{
    public int Id { get; set; }

    [Required]
    public int JobApprovalId { get; set; }

    public int PageNumber { get; set; } = 1;

    [Required]
    public string CanvasDataUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public JobApproval JobApproval { get; set; } = null!;
}
