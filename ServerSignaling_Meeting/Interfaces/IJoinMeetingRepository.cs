using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface IJoinMeetingRepository
    {
        Task<JoinMeeting?> GetJoinByIdAsync(Guid id);
        Task<IEnumerable<JoinMeeting>> GetParticipantsByRoomIdAsync(Guid roomId);
        Task<IEnumerable<JoinMeeting>> GetMeetingsByUserIdAsync(Guid userId);
        Task<JoinMeeting?> GetParticipantByUserAndRoomAsync(Guid userId, Guid roomId);
        Task<int> GetRoomActiveParticipantsCountAsync(Guid roomId);

        // JOIN & LEAVE
        Task<JoinMeeting> JoinMeetingAsync(Guid roomId, Guid userId, string role = "participant");
        Task LeaveMeetingAsync(Guid roomId, Guid userId);

        // CHECK
        Task<bool> IsUserInRoomAsync(Guid userId, Guid roomId);
        Task<bool> CanJoinRoomAsync(Guid roomId);
    }
}
