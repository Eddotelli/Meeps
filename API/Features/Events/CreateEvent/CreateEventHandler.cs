using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;

namespace API.Features.Events.CreateEvent;

public class CreateEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageStorageService _imageStorageService;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<CreateEventHandler> _logger;

    public CreateEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IImageStorageService imageStorageService,
        IHashIdService hashIdService,
        ILogger<CreateEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _imageStorageService = imageStorageService;
        _hashIdService = hashIdService;
        _logger = logger;
    }

    public async Task<Result<CreateEventResponse>> HandleAsync(CreateEventRequest request)
    {
        // Get current user ID from claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized attempt to create event");
            return Result.Failure<CreateEventResponse>(AuthErrors.InvalidCredentials);
        }

        // Verify category exists
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId!.Value);

        if (!categoryExists)
        {
            _logger.LogWarning("Category {CategoryId} not found", request.CategoryId);
            return Result.Failure<CreateEventResponse>(EventErrors.CategoryNotFound);
        }

        // Create new event (without image first)
        var newEvent = new Event
        {
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            DateTime = request.DateTime!.Value.ToUniversalTime(), // Convert to UTC
            CategoryId = request.CategoryId!.Value,
            MinAttendance = request.MinAttendance!.Value,
            MaxAttendance = request.MaxAttendance!.Value,
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            GenderRestriction = request.GenderRestriction!.Value,
            ImageUrl = request.ImageUrl,  // Use provided ImageUrl as fallback
            IsPublic = request.IsPublic,
            OnlyVerifiedUsers = request.OnlyVerifiedUsers,
            CreatedByUserId = userId,
            Status = EventStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Add creator as participant with Creator role
        var creatorParticipant = new EventParticipant
        {
            Event = newEvent,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.Creator
        };

        _context.Events.Add(newEvent);
        _context.EventParticipants.Add(creatorParticipant);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} created successfully by user {UserId} with creator participant", newEvent.Id, userId);

        // Now save the base64 image with the eventId if provided
        if (!string.IsNullOrEmpty(request.Base64Image))
        {
            _logger.LogInformation("Saving base64 image for event {EventId}", newEvent.Id);
            var saveResult = await _imageStorageService.SaveBase64ImageAsync(
                request.Base64Image,
                userId,
                Shared.Enums.ImageContext.Event,
                newEvent.Id);

            if (saveResult.IsSuccess)
            {
                newEvent.ImageUrl = saveResult.Value;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Base64 image saved successfully for event {EventId}: {ImageUrl}", newEvent.Id, saveResult.Value);
            }
            else
            {
                _logger.LogWarning("Failed to save base64 image for event {EventId}", newEvent.Id);
            }
        }

        return Result<CreateEventResponse>.Success(new CreateEventResponse
        {
            EventId = newEvent.Id,
            EventHash = _hashIdService.Encode(newEvent.Id),
            Title = newEvent.Title,
            DateTime = newEvent.DateTime,
            Message = "Event created successfully"
        });
    }
}
