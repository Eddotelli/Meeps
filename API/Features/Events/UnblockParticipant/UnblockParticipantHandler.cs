using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.UnblockParticipant;

public class UnblockParticipantHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IEventPermissionsService _permissionsService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UnblockParticipantHandler(
        ApplicationDbContext context,
        IEventPermissionsService permissionsService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _permissionsService = permissionsService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<UnblockParticipantResponse>> HandleAsync(UnblockParticipantRequest request)
    {
        // Get current user ID
        var currentUserIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserIdClaim) || !int.TryParse(currentUserIdClaim, out var currentUserId))
        {
            return Result.Failure<UnblockParticipantResponse>(CommonErrors.Unauthorized);
        }

        // Check if event exists
        var eventExists = await _context.Events.AnyAsync(e => e.Id == request.EventId);
        if (!eventExists)
        {
            return Result.Failure<UnblockParticipantResponse>(EventErrors.NotFound);
        }

        // Check if user has permission to manage participants
        var canManage = await _permissionsService.CanManageParticipants(currentUserId, request.EventId);
        if (!canManage)
        {
            return Result.Failure<UnblockParticipantResponse>(EventPermissionErrors.CannotManageParticipants);
        }

        // Get the participant to unblock
        var participant = await _context.EventParticipants
            .Include(ep => ep.User)
            .FirstOrDefaultAsync(ep => ep.EventId == request.EventId && ep.UserId == request.UserId);

        if (participant == null)
        {
            return Result.Failure<UnblockParticipantResponse>(EventPermissionErrors.ParticipantNotFound);
        }

        // Check if participant is actually blocked
        if (participant.Status != ParticipantStatus.Blocked)
        {
            return Result.Failure<UnblockParticipantResponse>(EventPermissionErrors.ParticipantNotBlocked);
        }

        // Unblock the participant by setting status back to Accepted
        participant.Status = ParticipantStatus.Accepted;
        participant.BlockedAt = null;
        participant.BlockedReason = null;
        participant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success(new UnblockParticipantResponse
        {
            EventId = request.EventId,
            UserId = request.UserId,
            DisplayName = participant.User.DisplayName ?? participant.User.Email ?? "Unknown",
            Message = "Participant unblocked successfully"
        });
    }
}
