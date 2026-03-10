using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;
using Shared.Enums;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<User> _userManager;
    private readonly IValidator<DeleteAccountRequest> _validator;
    private readonly IEmailService _emailService;
    private readonly ILogger<DeleteAccountHandler> _logger;

    public DeleteAccountHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<User> userManager,
        IValidator<DeleteAccountRequest> validator,
        IEmailService emailService,
        ILogger<DeleteAccountHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _validator = validator;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<DeleteAccountResponse>> Handle(DeleteAccountRequest request)
    {
        // 1. Get current user ID from JWT claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Delete account attempt with invalid user ID claim");
            return Result.Failure<DeleteAccountResponse>(UserErrors.Unauthorized);
        }

        // 2. Find user in database
        var user = await _context.Users
            .Include(u => u.CreatedEvents)
            .Include(u => u.Events)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("Delete account attempt for non-existent user: {UserId}", userId);
            return Result.Failure<DeleteAccountResponse>(UserErrors.NotFound);
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("Delete account attempt for already deleted user: {UserId}", userId);
            return Result.Failure<DeleteAccountResponse>(new Error(
                ErrorCodes.UserDeleteAccountFailed,
                "Account is already deleted",
                400
            ));
        }

        // 3. Verify password using UserManager
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Delete account failed - incorrect password for user: {UserId}", userId);
            return Result.Failure<DeleteAccountResponse>(new Error(
                ErrorCodes.UserIncorrectPassword,
                "Incorrect password",
                400
            ));
        }

        _logger.LogInformation("Starting account deletion for user: {UserId}", userId);

        int cancelledEventsCount = 0;
        int leftEventsCount = 0;
        int emailsSentCount = 0;

        // 4. Cancel all ACTIVE events created by user
        var activeEvents = await _context.Events
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .Where(e => e.CreatedByUserId == userId && 
                       e.Status == EventStatus.Active && 
                       !e.IsDeleted)
            .ToListAsync();

        foreach (var evt in activeEvents)
        {
            // Change event status to Cancelled
            evt.Status = EventStatus.Cancelled;
            evt.IsDeleted = true;
            evt.DeletedAt = DateTime.UtcNow;
            evt.DeletedByUserId = userId;

            cancelledEventsCount++;

            // Send email to all participants (except the creator)
            var participants = evt.EventParticipants
                .Where(ep => ep.UserId != userId && !string.IsNullOrEmpty(ep.User.Email))
                .ToList();

            foreach (var participant in participants)
            {
                try
                {
                    var emailResult = await _emailService.SendEventCancelledEmailAsync(
                        participant.User.Email!,
                        participant.User.DisplayName,
                        evt.Title,
                        "The event creator has deleted their account"
                    );

                    if (emailResult.IsSuccess)
                    {
                        emailsSentCount++;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to send cancellation email to {Email} for event {EventId}", 
                            participant.User.Email, 
                            evt.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, 
                        "Exception sending cancellation email to {Email} for event {EventId}", 
                        participant.User.Email, 
                        evt.Id
                    );
                }
            }

            _logger.LogInformation(
                "Cancelled event {EventId} - {EventTitle}. Sent {EmailCount} emails to participants",
                evt.Id,
                evt.Title,
                emailsSentCount
            );
        }

        // 5. Remove user from all events they are participating in (not created by them)
        var participations = await _context.EventParticipants
            .Include(ep => ep.Event)
            .Where(ep => ep.UserId == userId && 
                        ep.Event.CreatedByUserId != userId)
            .ToListAsync();

        foreach (var participation in participations)
        {
            _context.EventParticipants.Remove(participation);
            leftEventsCount++;
            
            _logger.LogInformation(
                "Removed user {UserId} from event {EventId}",
                userId,
                participation.EventId
            );
        }

        // 6. Send account deletion confirmation email to user (BEFORE anonymizing)
        if (!string.IsNullOrEmpty(user.Email))
        {
            try
            {
                var emailResult = await _emailService.SendAccountDeletedEmailAsync(
                    user.Email,
                    user.DisplayName,
                    cancelledEventsCount,
                    leftEventsCount
                );

                if (emailResult.IsSuccess)
                {
                    _logger.LogInformation("Account deletion confirmation email sent to {Email}", user.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send account deletion confirmation email to {Email}", user.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending account deletion confirmation email to {Email}", user.Email);
            }
        }

        // 7. Soft delete user
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        // 8. Anonymize user data
        var timestamp = DateTime.UtcNow.Ticks;
        user.DisplayName = "Deleted User";
        user.UserName = $"deleted_user_{userId}_{timestamp}";
        user.Email = $"deleted_{userId}_{timestamp}@deleted.local";
        user.NormalizedUserName = user.UserName.ToUpper();
        user.NormalizedEmail = user.Email.ToUpper();

        // Clear sensitive data
        user.Bio = null;
        user.ProfileImageUrl = null;
        user.DefaultCity = null;
        user.DefaultCityLatitude = null;
        user.DefaultCityLongitude = null;

        // 9. Revoke all refresh tokens (same as logout)
        var activeTokens = user.RefreshTokens
            .Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToList();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        // 10. Save all changes
        try
        {
            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Successfully deleted account for user {UserId}. " +
                "Cancelled {CancelledEvents} events, left {LeftEvents} events, sent {EmailsSent} emails",
                userId,
                cancelledEventsCount,
                leftEventsCount,
                emailsSentCount
            );

            return Result<DeleteAccountResponse>.Success(new DeleteAccountResponse
            {
                MessageKey = MessageKeys.AccountDeleted,
                CancelledEventsCount = cancelledEventsCount,
                LeftEventsCount = leftEventsCount,
                EmailsSentCount = emailsSentCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account for user {UserId}", userId);
            return Result.Failure<DeleteAccountResponse>(new Error(
                ErrorCodes.UserDeleteAccountFailed,
                "Failed to delete account. Please try again.",
                500
            ));
        }
    }
}
