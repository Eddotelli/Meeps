using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;
using Shared.Enums;
using Shared.Extensions;

namespace API.Features.Users.UpdateProfile;

public class UpdateProfileHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageStorageService _imageStorageService;
    private readonly IValidator<UpdateProfileRequest> _validator;
    private readonly ILogger<UpdateProfileHandler> _logger;

    public UpdateProfileHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IImageStorageService imageStorageService,
        IValidator<UpdateProfileRequest> validator,
        ILogger<UpdateProfileHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _imageStorageService = imageStorageService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<GetUserProfileResponse>> Handle(UpdateProfileRequest request)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<GetUserProfileResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Result.Failure<GetUserProfileResponse>(UserErrors.NotFound);
        }

        // Check if DisplayName is taken by another user
        if (request.DisplayName != user.DisplayName)
        {
            var displayNameTaken = await _context.Users
                .AnyAsync(u => u.DisplayName == request.DisplayName && u.Id != userId);

            if (displayNameTaken)
            {
                return Result.Failure<GetUserProfileResponse>(
                    new Error(ErrorCodes.UserDisplayNameExists, "Display name is already taken"));
            }
        }

        // Check for gender change constraints
        if (request.Gender.HasValue && user.Gender != request.Gender)
        {
            _logger.LogInformation("User {UserId} attempting to change gender from {OldGender} to {NewGender}",
                userId, user.Gender, request.Gender);

            var genderConflictResult = await CheckGenderChangeConflicts(userId, request.Gender.Value);
            if (genderConflictResult.IsFailure)
            {
                return Result.Failure<GetUserProfileResponse>(genderConflictResult.Error!);
            }
        }

        // Check for birth date change constraints
        if (request.BirthDate.HasValue && user.BirthDate != request.BirthDate)
        {
            _logger.LogInformation("User {UserId} attempting to change birth date from {OldDate} to {NewDate}",
                userId, user.BirthDate, request.BirthDate);

            var ageConflictResult = await CheckBirthDateChangeConflicts(userId, request.BirthDate.Value);
            if (ageConflictResult.IsFailure)
            {
                return Result.Failure<GetUserProfileResponse>(ageConflictResult.Error!);
            }
        }

        // If Base64Image is provided, save it to disk
        if (!string.IsNullOrEmpty(request.Base64Image))
        {
            _logger.LogInformation("Saving base64 profile image for user {UserId}", userId);
            var saveResult = await _imageStorageService.SaveBase64ImageAsync(
                request.Base64Image,
                userId,
                Shared.Enums.ImageContext.Profile);

            if (saveResult.IsSuccess)
            {
                user.ProfileImageUrl = saveResult.Value;
                _logger.LogInformation("Base64 profile image saved successfully: {ImageUrl}", user.ProfileImageUrl);
            }
            else
            {
                _logger.LogWarning("Failed to save base64 profile image for user {UserId}", userId);
            }
        }

        // Update user properties
        user.DisplayName = request.DisplayName;
        user.Bio = request.Bio;
        user.Gender = request.Gender;
        user.BirthDate = request.BirthDate;

        await _context.SaveChangesAsync();

        // Return updated profile
        var response = new GetUserProfileResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            BirthDate = user.BirthDate,
            Gender = user.Gender,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            IsVerified = user.IsVerified,
            EventsCreated = await _context.Events.CountAsync(e => e.CreatedByUserId == user.Id),
            EventsJoined = await _context.EventParticipants.CountAsync(ep => ep.UserId == user.Id),
            CategoriesCount = await _context.UserCategories.CountAsync(uc => uc.UserId == user.Id)
        };

        return Result.Success(response);
    }

    private async Task<Result> CheckGenderChangeConflicts(int userId, Gender newGender)
    {
        // Check if user has any events with gender-specific restrictions
        // If they do, they cannot change their gender regardless of what they change to
        var conflictingEvents = await _context.Events
            .Where(e => e.CreatedByUserId == userId)
            .Where(e => e.DateTime > DateTime.UtcNow) // Only future events
            .Where(e => e.GenderRestriction == GenderRestriction.MaleOnly ||
                       e.GenderRestriction == GenderRestriction.FemaleOnly ||
                       e.GenderRestriction == GenderRestriction.NonBinaryOnly)
            .Select(e => new { e.Title, e.DateTime })
            .ToListAsync();

        if (conflictingEvents.Any())
        {
            _logger.LogWarning("User {UserId} cannot change gender - {Count} events with conflicting gender restrictions",
                userId, conflictingEvents.Count);

            return Result.Failure(
                new Error(ErrorCodes.UserGenderConflictWithEvents,
                    $"Cannot change gender. You have {conflictingEvents.Count} event(s) with gender restrictions that require your current gender."));
        }

        return Result.Success();
    }

    private async Task<Result> CheckBirthDateChangeConflicts(int userId, DateTime newBirthDate)
    {
        // Calculate new age
        int newAge = CalculateAge(newBirthDate);

        // Check if user has created events with age restrictions that the new age would violate
        // Note: Events with MinAge=18 and MaxAge=99 (all ages) are not considered conflicts
        var conflictingEvents = await _context.Events
            .Where(e => e.CreatedByUserId == userId)
            .Where(e => e.DateTime > DateTime.UtcNow) // Only future events
            .Where(e => e.MinAge.HasValue || e.MaxAge.HasValue)
            .Where(e => !(e.MinAge == 18 && e.MaxAge == 99)) // Exclude "all ages" restrictions
            .Where(e => (e.MinAge.HasValue && newAge < e.MinAge.Value) ||
                       (e.MaxAge.HasValue && newAge > e.MaxAge.Value))
            .Select(e => new { e.Title, e.DateTime, e.MinAge, e.MaxAge })
            .ToListAsync();

        if (conflictingEvents.Any())
        {
            _logger.LogWarning("User {UserId} cannot change birth date - {Count} events with conflicting age restrictions. New age would be {NewAge}",
                userId, conflictingEvents.Count, newAge);

            return Result.Failure(
                new Error(ErrorCodes.UserAgeConflictWithEvents,
                    $"Cannot change birth date. Your new age ({newAge}) would violate age restrictions on {conflictingEvents.Count} event(s) you have created."));
        }

        return Result.Success();
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
            age--;
        return age;
    }
}
