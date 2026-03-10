using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Common.Constants;
using System.Security.Claims;

namespace API.Features.Events.DeleteEvent;

public class DeleteEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageStorageService _imageStorageService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DeleteEventHandler> _logger;

    public DeleteEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IImageStorageService imageStorageService,
        IWebHostEnvironment environment,
        ILogger<DeleteEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _imageStorageService = imageStorageService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Result<DeleteEventResponse>> HandleAsync(int eventId)
    {
        _logger.LogInformation("Delete request for event {EventId}", eventId);

        // Get current user ID
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized attempt to delete event {EventId}", eventId);
            return Result.Failure<DeleteEventResponse>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);

        // Find event - include related data and ignore soft delete filter
        var eventEntity = await _context.Events
            .IgnoreQueryFilters() // To check if already soft deleted
            .Include(e => e.EventParticipants)
            .Include(e => e.Messages)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found", eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.NotFound);
        }

        // Check if already soft deleted
        if (eventEntity.IsDeleted)
        {
            _logger.LogWarning("Event {EventId} is already deleted", eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.AlreadyDeleted);
        }

        // Check if user is the creator
        if (eventEntity.CreatedByUserId != userIdInt)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete event {EventId} but is not the creator",
                userIdInt, eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.Unauthorized);
        }

        var currentTime = DateTime.UtcNow;
        var eventHasPassed = eventEntity.DateTime < currentTime;

        if (eventHasPassed)
        {
            // SOFT DELETE - Event has already happened, keep for history
            _logger.LogInformation(
                "Soft deleting event {EventId} (event date: {EventDate} has passed)",
                eventId, eventEntity.DateTime);

            eventEntity.IsDeleted = true;
            eventEntity.DeletedAt = currentTime;
            eventEntity.DeletedByUserId = userIdInt;
            eventEntity.Status = Shared.Enums.EventStatus.Cancelled;

            // EventParticipants & Messages are kept (cascade delete does not trigger)
        }
        else
        {
            // HARD DELETE - Event hasn't happened yet, no purpose to keep data
            _logger.LogInformation(
                "Hard deleting event {EventId} (event date: {EventDate} is in future)",
                eventId, eventEntity.DateTime);

            // Remove event from database
            // CASCADE will automatically delete:
            // - EventParticipants
            // - Messages
            _context.Events.Remove(eventEntity);

            // Delete event image directory if exists
            var eventImageDirectory = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "users",
                eventEntity.CreatedByUserId.ToString(),
                "events",
                eventEntity.Id.ToString());

            if (Directory.Exists(eventImageDirectory))
            {
                try
                {
                    Directory.Delete(eventImageDirectory, recursive: true);
                    _logger.LogInformation("Deleted event image directory: {Directory}", eventImageDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete event image directory: {Directory}", eventImageDirectory);
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} deleted successfully (soft: {IsSoft})",
            eventId, eventHasPassed);

        return Result<DeleteEventResponse>.Success(new DeleteEventResponse
        {
            MessageKey = MessageKeys.EventDeleted
        });
    }
}
