using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Locations;

public class SearchLocationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 50)]
    public int Limit { get; set; } = 10;
}
