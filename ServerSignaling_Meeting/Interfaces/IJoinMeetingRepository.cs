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
        Task<Guid> GetUserIdByUsernameAsync(string userName);

        // JOIN & LEAVE
        Task<JoinMeeting> JoinMeetingAsync(Guid roomId, Guid userId, string role = "participant");
        Task LeaveMeetingAsync(Guid roomId, Guid userId);

        Task UpdateStatusAsync(Guid joinRoomId, Guid roomId, Guid userId, string status, DateTime? joinAt);

        Task UpdateAsync(JoinMeeting participant);

        // CHECK
        Task<bool> IsUserInRoomAsync(Guid userId, Guid roomId);
        Task<bool> CanJoinRoomAsync(Guid roomId);
    }
}
