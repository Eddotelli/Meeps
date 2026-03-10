using Shared.Enums;

namespace Shared.Contracts.Users;

public class GetUserProfileResponse
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsVerified { get; set; }
    public int EventsCreated { get; set; }
    public int EventsJoined { get; set; }
    public int CategoriesCount { get; set; }
}
