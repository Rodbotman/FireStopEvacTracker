using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class JobApproval
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }
    public EvacJob? Job { get; set; }

    public JobAnnotation? Annotation { get; set; }

    [Required]
    public string ClientEmail { get; set; } = string.Empty;

    [Required]
    public string ClientName { get; set; } = string.Empty;

    public string? ApproverName { get; set; }

    // Checklist responses
    public bool? LayoutAccuracyApproved { get; set; }
    public bool? FireEquipmentLocationsApproved { get; set; }
    public bool? YouAreHereApproved { get; set; }
    public bool? DiagramMountingLocationApproved { get; set; }
    public string? ChangesRequired { get; set; }

    // Approval status
    public bool IsApproved => LayoutAccuracyApproved == true &&
                              FireEquipmentLocationsApproved == true &&
                              YouAreHereApproved == true &&
                              DiagramMountingLocationApproved == true;

    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int ChecklistItemsCompleted
    {
        get
        {
            int count = 0;
            if (LayoutAccuracyApproved.HasValue) count++;
            if (FireEquipmentLocationsApproved.HasValue) count++;
            if (YouAreHereApproved.HasValue) count++;
            if (DiagramMountingLocationApproved.HasValue) count++;
            return count;
        }
    }
}
