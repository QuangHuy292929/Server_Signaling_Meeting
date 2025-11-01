using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface IRoomMeetingRepository
    {
        // GET
        Task<RoomMeeting?> GetRoomByIdAsync(Guid roomId);
        Task<RoomMeeting?> GetRoomByKeyAsync(string roomKey);
        Task<IEnumerable<RoomMeeting>> GetAllActiveRoomsAsync();
        Task<IEnumerable<RoomMeeting>> GetRoomsByUserIdAsync(Guid userId);
        Task<int> GetRoomParticipantsCountAsync(Guid roomId);

        // CREATE
        Task<RoomMeeting> CreateRoomAsync(string roomName, int max, Guid hostId);

        // UPDATE & DELETE
        Task UpdateRoomAsync(RoomMeeting room);
        Task DeleteRoomAsync(Guid roomId);
        Task<bool> IsRoomActiveAsync(Guid roomId);
    }
}
