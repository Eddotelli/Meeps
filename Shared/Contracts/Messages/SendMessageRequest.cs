using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Messages;

public class SendMessageRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}
