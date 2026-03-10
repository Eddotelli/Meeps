using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Events;

public class BlockParticipantRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}
