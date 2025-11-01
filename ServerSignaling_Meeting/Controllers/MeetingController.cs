// Controllers/MeetingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerSignaling_Meeting.Dtos;
using ServerSignaling_Meeting.Dtos.RoomDto;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;


namespace ServerSignaling_Meeting.Controllers
{
    [ApiController]
    [Route("api/meetings")]
    [Authorize]
    public class MeetingController : ControllerBase
    {
        private readonly IRoomMeetingRepository _roomRepo;
        private readonly IJoinMeetingRepository _joinRepo;
        private readonly ILogger<MeetingController> _logger;

        public MeetingController(
            IRoomMeetingRepository roomRepo,
            IJoinMeetingRepository joinRepo,
            ILogger<MeetingController> logger)
        {
            _roomRepo = roomRepo;
            _joinRepo = joinRepo;
            _logger = logger;
        }

        // ROOM MANAGEMENT
        /// Lấy tất cả rooms đang active
        [HttpGet("active-rooms")]
        public async Task<IActionResult> GetActiveRooms()
        {
            var rooms = await _roomRepo.GetAllActiveRoomsAsync();

            var result = rooms.Select(r => new
            {
                r.Id,
                r.RoomKey,
                r.RoomName,
                r.Max,
                r.CreateAt,
                r.IsActive,
                CurrentParticipants = r.JoinMeetings.Count(jm => jm.LeaveAt == null),
                AvailableSlots = r.Max - r.JoinMeetings.Count(jm => jm.LeaveAt == null)
            });

            return Ok(new { success = true, data = result });
        }

        /// Lấy rooms mà user đang tham gia
        [HttpGet("my-rooms")]
        public async Task<IActionResult> GetMyRooms()
        {
            var userId = User.GetCurrentUserId();
            var rooms = await _roomRepo.GetRoomsByUserIdAsync(userId);

            return Ok(new { success = true, data = rooms });
        }

        /// Lấy thông tin room theo ID
        [HttpGet("rooms/{roomId}")]
        public async Task<IActionResult> GetRoomById(Guid roomId)
        {
            var room = await _roomRepo.GetRoomByIdAsync(roomId);

            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });

            var participantsCount = await _roomRepo.GetRoomParticipantsCountAsync(roomId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    room.Id,
                    room.RoomKey,
                    room.RoomName,
                    room.Max,
                    room.CreateAt,
                    room.IsActive,
                    CurrentParticipants = participantsCount,
                    IsFull = participantsCount >= room.Max
                }
            });
        }

        /// Lấy thông tin room theo RoomKey (để join bằng link)
        [HttpGet("rooms/key/{roomKey}")]
        public async Task<IActionResult> GetRoomByKey(string roomKey)
        {
            var room = await _roomRepo.GetRoomByKeyAsync(roomKey);

            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });

            var participantsCount = await _roomRepo.GetRoomParticipantsCountAsync(room.Id);
            var canJoin = await _joinRepo.CanJoinRoomAsync(room.Id);

            return Ok(new
            {
                success = true,
                data = new
                {
                    room.Id,
                    room.RoomKey,
                    room.RoomName,
                    room.Max,
                    room.IsActive,
                    CurrentParticipants = participantsCount,
                    CanJoin = canJoin
                }
            });
        }

        /// Tạo room mới
        [HttpPost("rooms")]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
        {
            var userId = User.GetCurrentUserId();
            var username = User.GetUserName();

            if (string.IsNullOrWhiteSpace(request.RoomName))
                return BadRequest(new { success = false, message = "Room name is required" });

            if (request.MaxParticipants < 2 || request.MaxParticipants > 100)
                return BadRequest(new { success = false, message = "Max participants must be between 2 and 100" });

            var room = await _roomRepo.CreateRoomAsync(
                request.RoomName,
                request.MaxParticipants,
                userId);

            return Ok(new
            {
                success = true,
                message = "Room created successfully",
                data = new
                {
                    room.Id,
                    room.RoomKey,
                    room.RoomName,
                    room.Max,
                    room.CreateAt,
                    JoinUrl = $"/meetings/join/{room.RoomKey}",
                    CreatedBy = username
                }
            });
        }

        /// Cập nhật thông tin room
        [HttpPut("rooms/{roomId}")]
        public async Task<IActionResult> UpdateRoom(Guid roomId, [FromBody] UpdateRoomRequest request)
        {
            var userId = User.GetCurrentUserId();

            var room = await _roomRepo.GetRoomByIdAsync(roomId);
            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });

            // Kiểm tra quyền: chỉ host mới update được
            var participant = await _joinRepo.GetParticipantByUserAndRoomAsync(userId, roomId);
            if (participant?.Role != "host")
                return Forbid();

            // Update room info
            if (!string.IsNullOrWhiteSpace(request.RoomName))
                room.RoomName = request.RoomName;

            if (request.MaxParticipants.HasValue)
            {
                var currentCount = await _roomRepo.GetRoomParticipantsCountAsync(roomId);
                if (request.MaxParticipants < currentCount)
                    return BadRequest(new { success = false, message = "Cannot set max lower than current participants" });

                room.Max = request.MaxParticipants.Value;
            }

            await _roomRepo.UpdateRoomAsync(room);

            return Ok(new { success = true, message = "Room updated", data = room });
        }

        /// Xóa/đóng room
        [HttpDelete("rooms/{roomId}")]
        public async Task<IActionResult> DeleteRoom(Guid roomId)
        {
            var userId = User.GetCurrentUserId();

            var room = await _roomRepo.GetRoomByIdAsync(roomId);
            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });

            // Kiểm tra quyền: chỉ host mới xóa được
            var participant = await _joinRepo.GetParticipantByUserAndRoomAsync(userId, roomId);
            if (participant?.Role != "host")
                return Forbid();

            // Soft delete: set IsActive = false
            room.IsActive = false;
            await _roomRepo.UpdateRoomAsync(room);

            // Hoặc hard delete:
            // await _roomRepo.DeleteRoomAsync(roomId);

            return Ok(new { success = true, message = "Room closed" });
        }


        // PARTICIPANTS MANAGEMENT
        /// Join vào room
        [HttpPost("rooms/{roomId}/join")]
        public async Task<IActionResult> JoinRoom(Guid roomId)
        {
            var userId = User.GetCurrentUserId();
            var username = User.GetUserName();

            // Kiểm tra room có tồn tại và active không
            var room = await _roomRepo.GetRoomByIdAsync(roomId);
            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });

            if (!room.IsActive)
                return BadRequest(new { success = false, message = "Room is not active" });

            // Kiểm tra có thể join không (full chưa)
            var canJoin = await _joinRepo.CanJoinRoomAsync(roomId);
            if (!canJoin)
                return BadRequest(new { success = false, message = "Room is full" });

            // Kiểm tra user đã trong room chưa
            var isInRoom = await _joinRepo.IsUserInRoomAsync(userId, roomId);
            if (isInRoom)
                return BadRequest(new { success = false, message = "Already in room" });

            // Join room
            var joinMeeting = await _joinRepo.JoinMeetingAsync(roomId, userId, "participant");

            return Ok(new
            {
                success = true,
                message = "Joined room successfully",
                data = new
                {
                    joinMeeting.Id,
                    joinMeeting.RoomId,
                    joinMeeting.UserId,
                    Username = username,
                    joinMeeting.Role,
                    joinMeeting.JoinAt,
                    Room = new
                    {
                        room.RoomName,
                        room.RoomKey
                    }
                }
            });
        }

        /// Join room bằng RoomKey (từ link invite)
        [HttpPost("join/{roomKey}")]
        public async Task<IActionResult> JoinRoomByKey(string roomKey)
        {
            var room = await _roomRepo.GetRoomByKeyAsync(roomKey);
            if (room == null)
                return NotFound(new { success = false, message = "Invalid room key" });

            // Redirect to join by ID
            return await JoinRoom(room.Id);
        }

        /// Leave room
        [HttpPost("rooms/{roomId}/leave")]
        public async Task<IActionResult> LeaveRoom(Guid roomId)
        {
            var userId = User.GetCurrentUserId();

            // Kiểm tra user có trong room không
            var isInRoom = await _joinRepo.IsUserInRoomAsync(userId, roomId);
            if (!isInRoom)
                return BadRequest(new { success = false, message = "Not in room" });

            await _joinRepo.LeaveMeetingAsync(roomId, userId);

            return Ok(new { success = true, message = "Left room" });
        }

        /// Lấy danh sách participants trong room
        [HttpGet("rooms/{roomId}/participants")]
        public async Task<IActionResult> GetParticipants(Guid roomId)
        {
            var userId = User.GetCurrentUserId();

            // Kiểm tra user có trong room không (chỉ người trong room mới xem được)
            var isInRoom = await _joinRepo.IsUserInRoomAsync(userId, roomId);
            if (!isInRoom)
                return Forbid();

            var participants = await _joinRepo.GetParticipantsByRoomIdAsync(roomId);
            var count = await _joinRepo.GetRoomActiveParticipantsCountAsync(roomId);

            var result = participants.Select(p => new
            {
                p.Id,
                p.UserId,
                Username = p.User?.UserName,
                Email = p.User?.Email,
                p.Role,
                p.JoinAt,
                IsHost = p.Role == "host"
            });

            return Ok(new
            {
                success = true,
                data = result,
                totalCount = count
            });
        }

        /// Kick participant (chỉ host)
        [HttpPost("rooms/{roomId}/participants/{participantUserId}/kick")]
        public async Task<IActionResult> KickParticipant(Guid roomId, Guid participantUserId)
        {
            var userId = User.GetCurrentUserId();

            // Kiểm tra quyền: chỉ host mới kick được
            var host = await _joinRepo.GetParticipantByUserAndRoomAsync(userId, roomId);
            if (host?.Role != "host")
                return Forbid();

            // Không thể kick chính mình
            if (userId == participantUserId)
                return BadRequest(new { success = false, message = "Cannot kick yourself" });

            // Kick participant
            await _joinRepo.LeaveMeetingAsync(roomId, participantUserId);

            return Ok(new { success = true, message = "Participant kicked" });
        }

        /// <summary>
        /// Lấy lịch sử meetings của user
        /// </summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyMeetingHistory()
        {
            var userId = User.GetCurrentUserId();
            var meetings = await _joinRepo.GetMeetingsByUserIdAsync(userId);

            var result = meetings.Select(m => new
            {
                m.Id,
                m.RoomId,
                RoomName = m.RoomMeeting?.RoomName,
                RoomKey = m.RoomMeeting?.RoomKey,
                m.Role,
                m.JoinAt,
                m.LeaveAt,

                Duration = m.LeaveAt.HasValue ? (double?)(m.LeaveAt.Value - m.JoinAt).TotalMinutes : null
            });

            return Ok(new { success = true, data = result });
        }
    }
}