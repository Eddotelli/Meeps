using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.BlockParticipant;

public class BlockParticipantHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IEventPermissionsService _permissionsService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BlockParticipantHandler(
        ApplicationDbContext context,
        IEventPermissionsService permissionsService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _permissionsService = permissionsService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<BlockParticipantResponse>> HandleAsync(BlockParticipantRequest request)
    {
        // Get current user ID
        var currentUserIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserIdClaim) || !int.TryParse(currentUserIdClaim, out var currentUserId))
        {
            return Result.Failure<BlockParticipantResponse>(CommonErrors.Unauthorized);
        }

        // Check if event exists
        var eventExists = await _context.Events.AnyAsync(e => e.Id == request.EventId);
        if (!eventExists)
        {
            return Result.Failure<BlockParticipantResponse>(EventErrors.NotFound);
        }

        // Check if user has permission to manage participants
        var canManage = await _permissionsService.CanManageParticipants(currentUserId, request.EventId);
        if (!canManage)
        {
            return Result.Failure<BlockParticipantResponse>(EventPermissionErrors.CannotManageParticipants);
        }

        // Check if trying to block self
        if (request.UserId == currentUserId)
        {
            return Result.Failure<BlockParticipantResponse>(EventPermissionErrors.CannotBlockSelf);
        }

        // Get the participant to block
        var participant = await _context.EventParticipants
            .Include(ep => ep.User)
            .FirstOrDefaultAsync(ep => ep.EventId == request.EventId && ep.UserId == request.UserId);

        if (participant == null)
        {
            return Result.Failure<BlockParticipantResponse>(EventPermissionErrors.ParticipantNotFound);
        }

        // Cannot block the event creator
        if (participant.Role == EventRole.Creator)
        {
            return Result.Failure<BlockParticipantResponse>(EventPermissionErrors.CannotBlockCreator);
        }

        // Check if already blocked
        if (participant.Status == ParticipantStatus.Blocked)
        {
            return Result.Failure<BlockParticipantResponse>(EventPermissionErrors.ParticipantAlreadyBlocked);
        }

        // Block the participant
        participant.Status = ParticipantStatus.Blocked;
        participant.BlockedAt = DateTime.UtcNow;
        participant.BlockedReason = request.Reason;
        participant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success(new BlockParticipantResponse
        {
            EventId = request.EventId,
            UserId = request.UserId,
            DisplayName = participant.User.DisplayName ?? participant.User.Email ?? "Unknown",
            Message = "Participant blocked successfully"
        });
    }
}
