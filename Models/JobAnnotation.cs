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

    /// <summary>
    /// Relative path to a snapshot copy of the source diagram page PNG taken
    /// at save time. Lets the admin see what the client was looking at when
    /// they made the markup, even if the job's PDF gets replaced later.
    /// </summary>
    public string? SnapshotImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public JobApproval JobApproval { get; set; } = null!;
}
