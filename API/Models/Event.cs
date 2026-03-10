using Shared.Enums;

namespace API.Models;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int MinAttendance { get; set; }
    public int MaxAttendance { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public GenderRestriction GenderRestriction { get; set; } = GenderRestriction.None;
    public EventStatus Status { get; set; } = EventStatus.Active;
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; } = true; public bool OnlyVerifiedUsers { get; set; } = false;
    
    // Soft Delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedByUserId { get; set; }
    public User? DeletedByUser { get; set; }
    
    public ICollection<EventParticipant> EventParticipants { get; set; } = new List<EventParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
