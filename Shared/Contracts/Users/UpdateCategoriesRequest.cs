using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Users;

public class UpdateCategoriesRequest
{
    public int[] CategoryIds { get; set; } = Array.Empty<int>();
}
