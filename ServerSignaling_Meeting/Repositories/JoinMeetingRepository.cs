using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Data;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Repositories
{
    public class JoinMeetingRepository : IJoinMeetingRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JoinMeetingRepository> _logger;

        public JoinMeetingRepository(ApplicationDbContext context, ILogger<JoinMeetingRepository> logger)
        {
            _context = context;
            _logger = logger;
        }


        //GET JOIN MEETING--------------------------------
        public async Task<JoinMeeting?> GetJoinByIdAsync(Guid id)
        {
            return await _context.JoinMeetings
                .Include(j => j.User)
                .Include(j => j.RoomMeeting)
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        public async Task<IEnumerable<JoinMeeting>> GetParticipantsByRoomIdAsync(Guid roomId)
        {
            return await _context.JoinMeetings
                .Where(jm => jm.RoomId == roomId && jm.LeaveAt == null)
                .Include(j => j.User)
                .ToListAsync();
        }

        public async Task<IEnumerable<JoinMeeting>> GetMeetingsByUserIdAsync(Guid userId)
        {
            return await _context.JoinMeetings
                .Where(jm => jm.UserId == userId)
                .Include(j => j.RoomMeeting)
                .ToListAsync();
        }

        public async Task<JoinMeeting?> GetParticipantByUserAndRoomAsync(Guid userId, Guid roomId)
        {
            return await _context.JoinMeetings
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.UserId == userId && j.RoomId == roomId && j.LeaveAt == null);
        }

        public async Task<int> GetRoomActiveParticipantsCountAsync(Guid roomId)
        {
            return await _context.JoinMeetings
                .Where(jm => jm.RoomId == roomId && jm.LeaveAt == null)
                .CountAsync();
        }


        //JOIN MEETING--------------------------------
        public async Task<JoinMeeting> JoinMeetingAsync(Guid roomId, Guid userId, string role = "participant")
        {
            // Kiểm tra user có đang trong room không
            var existing = await GetParticipantByUserAndRoomAsync(userId, roomId);
            if (existing != null)
            {
                _logger.LogWarning($"User {userId} is already in room {roomId}");
                return existing;
            }

            var joinMeeting = new JoinMeeting
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = userId,
                Role = role,
                JoinAt = DateTime.UtcNow
            };

            _context.JoinMeetings.Add(joinMeeting);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} joined room {roomId}");
            return joinMeeting;
        }


        //LEAVE MEETING--------------------------------
        public async Task LeaveMeetingAsync(Guid roomId, Guid userId)
        {
            var joinMeeting = await GetParticipantByUserAndRoomAsync(userId, roomId);
            if (joinMeeting != null)
            {
                joinMeeting.LeaveAt = DateTime.UtcNow;
                _context.JoinMeetings.Update(joinMeeting);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} left room {roomId}");
            }
        }


        //VALIDATIONS--------------------------------
        public async Task<bool> IsUserInRoomAsync(Guid userId, Guid roomId)
        {
            return await _context.JoinMeetings
                .AnyAsync(jm => jm.UserId == userId && jm.RoomId == roomId && jm.LeaveAt == null);
        }

        public async Task<bool> CanJoinRoomAsync(Guid roomId)
        {
            var room = await _context.RoomMeetings
                .Include(r => r.JoinMeetings)
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room == null || !room.IsActive)
                return false;

            var activeCount = room.JoinMeetings.Count(jm => jm.LeaveAt == null);
            return activeCount < room.Max;
        }
    }
}
