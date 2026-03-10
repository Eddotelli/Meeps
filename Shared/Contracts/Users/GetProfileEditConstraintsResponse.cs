namespace Shared.Contracts.Users;

public class GetProfileEditConstraintsResponse
{
    /// <summary>
    /// Whether the user can change their gender
    /// </summary>
    public bool CanChangeGender { get; set; }

    /// <summary>
    /// Whether the user can change their birth date
    /// </summary>
    public bool CanChangeBirthDate { get; set; }

    /// <summary>
    /// List of events that prevent gender change (with their gender restrictions)
    /// </summary>
    public List<ConflictingEvent> GenderConflictingEvents { get; set; } = new();

    /// <summary>
    /// List of events that prevent age change (with their age restrictions)
    /// </summary>
    public List<ConflictingEvent> AgeConflictingEvents { get; set; } = new();

    /// <summary>
    /// Warning messages to display to the user
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

public class ConflictingEvent
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public Shared.Enums.GenderRestriction? GenderRestriction { get; set; }
}
