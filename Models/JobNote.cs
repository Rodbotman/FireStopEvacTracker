using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class JobNote
{
    public int Id { get; set; }

    public int EvacJobId { get; set; }

    public EvacJob? EvacJob { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public string AddedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
