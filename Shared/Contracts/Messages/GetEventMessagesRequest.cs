using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Messages;

public class GetEventMessagesRequest
{
    [Required]
    public string EventHash { get; set; } = string.Empty;

    // Optional pagination parameters
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 50;
}
