using Shared.Enums;

namespace Shared.Contracts.Events;

public class GetEventEditConstraintsResponse
{
    /// <summary>
    /// Minimum allowed age based on youngest participant
    /// </summary>
    public int? MinAllowedAge { get; set; }

    /// <summary>
    /// Maximum allowed age based on oldest participant
    /// </summary>
    public int? MaxAllowedAge { get; set; }

    /// <summary>
    /// Whether event has participants with different genders
    /// </summary>
    public bool HasMixedGenders { get; set; }

    /// <summary>
    /// List of gender restrictions that don't exclude current participants
    /// </summary>
    public List<GenderRestriction> AllowedGenderRestrictions { get; set; } = new();

    /// <summary>
    /// Minimum value for MaxAttendance (current participant count)
    /// </summary>
    public int MinAllowedMaxAttendance { get; set; }

    /// <summary>
    /// Whether event has any unverified participants
    /// </summary>
    public bool HasUnverifiedParticipants { get; set; }

    /// <summary>
    /// Whether OnlyVerifiedUsers can be enabled
    /// </summary>
    public bool CanRequireVerifiedUsers { get; set; }

    /// <summary>
    /// Current number of active participants (not including blocked)
    /// </summary>
    public int CurrentParticipantCount { get; set; }

    /// <summary>
    /// List of constraint warnings to display to user
    /// </summary>
    public List<ConstraintWarning> Warnings { get; set; } = new();
}
