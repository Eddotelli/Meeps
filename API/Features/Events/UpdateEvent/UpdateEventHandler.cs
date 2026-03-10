using API.Infrastructure.Data;
using API.Infrastructure.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using System.Security.Claims;

namespace API.Features.Events.UpdateEvent;

public class UpdateEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageStorageService _imageStorageService;
    private readonly IEmailService _emailService;
    private readonly ILogger<UpdateEventHandler> _logger;

    public UpdateEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IImageStorageService imageStorageService,
        IEmailService emailService,
        ILogger<UpdateEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _imageStorageService = imageStorageService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<UpdateEventResponse>> HandleAsync(UpdateEventRequest request)
    {
        _logger.LogInformation("Updating event {EventId}", request.EventId);

        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized attempt to update event {EventId}", request.EventId);
            return Result.Failure<UpdateEventResponse>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);

        var eventEntity = await _context.Events
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .FirstOrDefaultAsync(e => e.Id == request.EventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for update", request.EventId);
            return Result.Failure<UpdateEventResponse>(EventErrors.NotFound);
        }

        // Check if user is the creator
        if (eventEntity.CreatedByUserId != userIdInt)
        {
            _logger.LogWarning("User {UserId} attempted to update event {EventId} but is not the creator", userIdInt, request.EventId);
            return Result.Failure<UpdateEventResponse>(EventErrors.Unauthorized);
        }

        // Verify category exists
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId);

        if (!categoryExists)
        {
            _logger.LogWarning("Category {CategoryId} not found for event {EventId} update", request.CategoryId, request.EventId);
            return Result.Failure<UpdateEventResponse>(EventErrors.CategoryNotFound);
        }

        // Store original values for change tracking
        var originalTitle = eventEntity.Title;
        var originalDateTime = eventEntity.DateTime;
        var originalLocation = eventEntity.Location;
        var originalMinAge = eventEntity.MinAge;
        var originalMaxAge = eventEntity.MaxAge;
        var originalGenderRestriction = eventEntity.GenderRestriction;
        var originalMaxAttendance = eventEntity.MaxAttendance;
        var originalOnlyVerifiedUsers = eventEntity.OnlyVerifiedUsers;

        // If Base64Image is provided, save it to disk
        string? savedImageUrl = null;
        if (!string.IsNullOrEmpty(request.Base64Image))
        {
            _logger.LogInformation("Saving base64 image for event {EventId}", request.EventId);
            var saveResult = await _imageStorageService.SaveBase64ImageAsync(
                request.Base64Image,
                userIdInt,
                Shared.Enums.ImageContext.Event,
                eventEntity.Id);

            if (saveResult.IsSuccess)
            {
                savedImageUrl = saveResult.Value;
                _logger.LogInformation("Base64 image saved successfully: {ImageUrl}", savedImageUrl);
            }
            else
            {
                _logger.LogWarning("Failed to save base64 image, using ImageUrl instead");
            }
        }

        // Update event
        eventEntity.Title = request.Title;
        eventEntity.Description = request.Description;
        eventEntity.Location = request.Location;
        eventEntity.Latitude = request.Latitude;
        eventEntity.Longitude = request.Longitude;
        eventEntity.DateTime = request.DateTime!.Value.ToUniversalTime(); // Convert to UTC
        eventEntity.CategoryId = request.CategoryId!.Value;
        eventEntity.MinAttendance = request.MinAttendance!.Value;
        eventEntity.MaxAttendance = request.MaxAttendance!.Value;
        eventEntity.MinAge = request.MinAge;
        eventEntity.MaxAge = request.MaxAge;
        eventEntity.GenderRestriction = request.GenderRestriction!.Value;
        eventEntity.ImageUrl = savedImageUrl ?? request.ImageUrl;  // Use saved base64 image if available
        eventEntity.IsPublic = request.IsPublic;
        eventEntity.OnlyVerifiedUsers = request.OnlyVerifiedUsers;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} updated successfully by user {UserId}", eventEntity.Id, userIdInt);

        // Send email notifications to participants if significant changes were made
        await NotifyParticipantsOfUpdate(
            eventEntity,
            originalTitle,
            originalDateTime,
            originalLocation,
            originalMinAge,
            originalMaxAge,
            originalGenderRestriction,
            originalMaxAttendance,
            originalOnlyVerifiedUsers);

        return Result.Success(new UpdateEventResponse
        {
            EventId = eventEntity.Id,
            Message = "Event updated successfully"
        });
    }

    private async Task NotifyParticipantsOfUpdate(
        API.Models.Event eventEntity,
        string originalTitle,
        DateTime originalDateTime,
        string originalLocation,
        int? originalMinAge,
        int? originalMaxAge,
        Shared.Enums.GenderRestriction originalGenderRestriction,
        int originalMaxAttendance,
        bool originalOnlyVerifiedUsers)
    {
        var changes = BuildChangeList(
            eventEntity,
            originalTitle,
            originalDateTime,
            originalLocation,
            originalMinAge,
            originalMaxAge,
            originalGenderRestriction,
            originalMaxAttendance,
            originalOnlyVerifiedUsers);

        // Only send emails if there are significant changes
        if (!changes.Any())
        {
            _logger.LogInformation("No significant changes to event {EventId}, skipping participant notifications", eventEntity.Id);
            return;
        }

        var activeParticipants = eventEntity.EventParticipants
            .Where(p => p.Status == Shared.Enums.ParticipantStatus.Accepted && p.UserId != eventEntity.CreatedByUserId)
            .ToList();

        _logger.LogInformation("Notifying {Count} participants about event {EventId} update", activeParticipants.Count, eventEntity.Id);

        foreach (var participant in activeParticipants)
        {
            if (participant.User?.Email != null)
            {
                await _emailService.SendEventUpdatedEmailAsync(
                    participant.User.Email,
                    participant.User.DisplayName,
                    eventEntity.Title,
                    changes);
            }
        }
    }

    private List<string> BuildChangeList(
        API.Models.Event eventEntity,
        string originalTitle,
        DateTime originalDateTime,
        string originalLocation,
        int? originalMinAge,
        int? originalMaxAge,
        Shared.Enums.GenderRestriction originalGenderRestriction,
        int originalMaxAttendance,
        bool originalOnlyVerifiedUsers)
    {
        var changes = new List<string>();

        if (eventEntity.Title != originalTitle)
            changes.Add($"Title: {originalTitle} → {eventEntity.Title}");

        if (eventEntity.DateTime != originalDateTime)
            changes.Add($"Date/Time: {originalDateTime:yyyy-MM-dd HH:mm} → {eventEntity.DateTime:yyyy-MM-dd HH:mm}");

        if (eventEntity.Location != originalLocation)
            changes.Add($"Location: {originalLocation} → {eventEntity.Location}");

        if (eventEntity.MinAge != originalMinAge || eventEntity.MaxAge != originalMaxAge)
        {
            var oldRange = $"{originalMinAge ?? 18}-{originalMaxAge ?? 99}";
            var newRange = $"{eventEntity.MinAge ?? 18}-{eventEntity.MaxAge ?? 99}";
            changes.Add($"Age range: {oldRange} → {newRange}");
        }

        if (eventEntity.GenderRestriction != originalGenderRestriction)
            changes.Add($"Gender restriction: {originalGenderRestriction} → {eventEntity.GenderRestriction}");

        if (eventEntity.MaxAttendance != originalMaxAttendance)
            changes.Add($"Max participants: {originalMaxAttendance} → {eventEntity.MaxAttendance}");

        if (eventEntity.OnlyVerifiedUsers != originalOnlyVerifiedUsers)
        {
            var oldValue = originalOnlyVerifiedUsers ? "Verified only" : "All users";
            var newValue = eventEntity.OnlyVerifiedUsers ? "Verified only" : "All users";
            changes.Add($"Participant requirements: {oldValue} → {newValue}");
        }

        return changes;
    }
}
