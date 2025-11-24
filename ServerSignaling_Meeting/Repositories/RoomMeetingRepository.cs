using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Data;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Repositories
{
    public class RoomMeetingRepository : IRoomMeetingRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RoomMeetingRepository> _logger;

        public RoomMeetingRepository(ApplicationDbContext context, ILogger<RoomMeetingRepository> logger)
        {
            _context = context;
            _logger = logger;
        }


        // GET ROOM--------------------------------
        public async Task<RoomMeeting?> GetRoomByIdAsync(Guid roomId)
        {
            return await _context.RoomMeetings
                .Include(r => r.JoinMeetings)
                .FirstOrDefaultAsync(r => r.Id == roomId);
        }

        public async Task<RoomMeeting?> GetRoomByKeyAsync(string roomKey)
        {
            return await _context.RoomMeetings
                .FirstOrDefaultAsync(r => r.RoomKey == roomKey);
        }

        public async Task<IEnumerable<RoomMeeting>> GetAllActiveRoomsAsync()
        {
            return await _context.RoomMeetings
                .Where(r => r.IsActive)
                .Include(r => r.JoinMeetings)
                .ToListAsync();
        }

        public async Task<IEnumerable<RoomMeeting>> GetRoomsByUserIdAsync(Guid userId)
        {
            return await _context.RoomMeetings
                .Where(r => r.JoinMeetings.Any(jm => jm.UserId == userId && jm.LeaveAt == null))
                .Include(r => r.JoinMeetings)
                .ToListAsync();
        }

        public async Task<int> GetRoomParticipantsCountAsync(Guid roomId)
        {
            return await _context.JoinMeetings
                .Where(jm => jm.RoomId == roomId && jm.LeaveAt == null)
                .CountAsync();
        }

        private string GenerateRoomKey()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            var rand = new Random();

            string RandomPart()
            {
                return new string(Enumerable.Range(0, 3)
                    .Select(_ => chars[rand.Next(chars.Length)])
                    .ToArray());
            }

            return $"{RandomPart()}-{RandomPart()}-{RandomPart()}";
        }


        //CREATE ROOM---------------------------------
        public async Task<RoomMeeting> CreateRoomAsync(string roomName, int max, Guid hostId)
        {
            var roomKey = GenerateRoomKey();
            var room = new RoomMeeting
            {
                Id = Guid.NewGuid(),
                RoomKey = roomKey,
                RoomName = roomName,
                Max = max,
                CreateAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.RoomMeetings.Add(room);
            await _context.SaveChangesAsync();

            // Add host to room
            var joinMeeting = new JoinMeeting
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                UserId = hostId,
                Role = "host",
                status = "joined",
                JoinAt = DateTime.UtcNow
            };

            _context.JoinMeetings.Add(joinMeeting);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Room '{roomName}' created with key {roomKey}");
            return room;
        }


        //UPDATE & DELETE ROOM------------------------
        public async Task UpdateRoomAsync(RoomMeeting room)
        {
            _context.RoomMeetings.Update(room);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRoomAsync(Guid roomId)
        {
            var room = await GetRoomByIdAsync(roomId);
            if (room != null)
            {
                _context.RoomMeetings.Remove(room);
                await _context.SaveChangesAsync();
            }
        }


        // ADDITIONAL METHODS-----------------------------
        public async Task<bool> IsRoomActiveAsync(Guid roomId)
        {
            var room = await GetRoomByIdAsync(roomId);
            return room?.IsActive ?? false;
        }
    }
}
