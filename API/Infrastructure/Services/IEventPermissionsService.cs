namespace API.Infrastructure.Services;

public interface IEventPermissionsService
{
    Task<bool> CanEditEvent(int userId, int eventId);
    Task<bool> CanDeleteEvent(int userId, int eventId);
    Task<bool> CanInviteUsers(int userId, int eventId);
    Task<bool> CanManageParticipants(int userId, int eventId);
    Task<bool> CanAccessChat(int userId, int eventId);
}
