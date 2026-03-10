using Microsoft.AspNetCore.Identity;
using Shared.Enums;

namespace API.Models;

public class User : IdentityUser<int>
{
    // Identity properties inherited: Id, UserName, Email, PasswordHash, etc.

    // Custom properties
    public string DisplayName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public bool IsVerified { get; set; } = false;
    public bool AcceptedTerms { get; set; } = false;
    public Gender? Gender { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }

    // User preferences for event discovery
    public string? DefaultCity { get; set; }
    public double? DefaultCityLatitude { get; set; }
    public double? DefaultCityLongitude { get; set; }
    public int SearchRadius { get; set; } = 25; // Default 25 km

    // Email verification
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    // Password reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Soft Delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
    public ICollection<EventParticipant> Events { get; set; } = new List<EventParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<UserCategory> Categories { get; set; } = new List<UserCategory>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
