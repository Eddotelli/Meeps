using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace API.Infrastructure.Services;

public class EventPermissionsService : IEventPermissionsService
{
    private readonly ApplicationDbContext _context;

    public EventPermissionsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanEditEvent(int userId, int eventId)
    {
        // Only Creator and CoOrganizer can edit event
        var participant = await GetParticipant(userId, eventId);

        return participant != null &&
               (participant.Role == EventRole.Creator ||
                participant.Role == EventRole.CoOrganizer);
    }

    public async Task<bool> CanDeleteEvent(int userId, int eventId)
    {
        // Only Creator can delete event
        var participant = await GetParticipant(userId, eventId);

        return participant != null && participant.Role == EventRole.Creator;
    }

    public async Task<bool> CanInviteUsers(int userId, int eventId)
    {
        // Creator and CoOrganizer can invite users
        var participant = await GetParticipant(userId, eventId);

        return participant != null &&
               (participant.Role == EventRole.Creator ||
                participant.Role == EventRole.CoOrganizer);
    }

    public async Task<bool> CanManageParticipants(int userId, int eventId)
    {
        // Creator and CoOrganizer can manage participants (remove, change roles)
        var participant = await GetParticipant(userId, eventId);

        return participant != null &&
               (participant.Role == EventRole.Creator ||
                participant.Role == EventRole.CoOrganizer);
    }

    public async Task<bool> CanAccessChat(int userId, int eventId)
    {
        // Any accepted participant can access chat
        // Only query Status to avoid missing Role column issue
        return await _context.EventParticipants
            .Where(ep => ep.UserId == userId && ep.EventId == eventId)
            .Select(ep => ep.Status)
            .FirstOrDefaultAsync() == ParticipantStatus.Accepted;
    }

    private async Task<Models.EventParticipant?> GetParticipant(int userId, int eventId)
    {
        return await _context.EventParticipants
            .FirstOrDefaultAsync(ep => ep.UserId == userId && ep.EventId == eventId);
    }
}
