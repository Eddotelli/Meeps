using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Events;

public class LeaveEventRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }
}
