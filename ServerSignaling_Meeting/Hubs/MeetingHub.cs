using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Interfaces;
using System.Collections.Concurrent;

namespace ServerSignaling_Meeting.Hubs
{
    [Authorize]
    public class MeetingHub : Hub
    {
        private readonly IRoomMeetingRepository _roomRepo;
        private readonly IJoinMeetingRepository _joinRepo;
        private readonly ILogger<MeetingHub> _logger;

        // ⭐ Tracking connections: roomId -> List<ConnectionInfo>
        private static readonly ConcurrentDictionary<Guid, List<ConnectionInfo>> _roomConnections = new();

        public MeetingHub(
            IRoomMeetingRepository roomRepo,
            IJoinMeetingRepository joinRepo,
            ILogger<MeetingHub> logger)
        {
            _roomRepo = roomRepo;
            _joinRepo = joinRepo;
            _logger = logger;
        }

        public class ConnectionInfo
        {
            public string ConnectionId { get; set; }
            public Guid UserId { get; set; }
            public string Username { get; set; }
            public DateTime JoinedAt { get; set; }
        }


        // ============================================
        // JOIN & LEAVE ROOM
        // ============================================

        /// <summary>
        /// Join vào room (client gọi sau khi đã join qua API)
        /// </summary>
        public async Task JoinRoom(Guid roomId)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            try
            {
                // Kiểm tra quyền join
                var isInRoom = await _joinRepo.IsUserInRoomAsync(userId, roomId);
                if (!isInRoom)
                {
                    await Clients.Caller.SendAsync("Error", "You must join room via API first");
                    return;
                }

                // Add vào SignalR group
                await Groups.AddToGroupAsync(connectionId, roomId.ToString());

                // Track connection
                if (!_roomConnections.ContainsKey(roomId))
                {
                    _roomConnections[roomId] = new List<ConnectionInfo>();
                }

                var connInfo = new ConnectionInfo
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    Username = username,
                    JoinedAt = DateTime.UtcNow
                };

                _roomConnections[roomId].Add(connInfo);

                // Lấy danh sách participants hiện tại (trước khi notify)
                var existingParticipants = _roomConnections[roomId]
                    .Where(c => c.ConnectionId != connectionId)
                    .Select(c => new
                    {
                        c.ConnectionId,
                        c.UserId,
                        c.Username
                    })
                    .ToList();

                // Gửi danh sách existing participants cho user mới join
                await Clients.Caller.SendAsync("ExistingParticipants", existingParticipants);

                // Thông báo cho tất cả người khác là có user mới join
                await Clients.OthersInGroup(roomId.ToString()).SendAsync("UserJoined", new
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    Username = username,
                    JoinedAt = DateTime.UtcNow
                });

                _logger.LogInformation($"User {username} ({userId}) joined room {roomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining room {roomId}");
                await Clients.Caller.SendAsync("Error", "Failed to join room");
            }
        }

        /// <summary>
        /// Leave room
        /// </summary>
        public async Task LeaveRoom(Guid roomId)
        {
            var userId = Context.User.GetCurrentUserId();
            var connectionId = Context.ConnectionId;

            await RemoveFromRoom(roomId, connectionId, userId);
        }


        // ============================================
        // WEBRTC SIGNALING
        // ============================================

        /// <summary>
        /// Gửi WebRTC Offer đến peer cụ thể
        /// </summary>
        public async Task SendOffer(string toConnectionId, object offer)
        {
            var fromUserId = Context.User.GetCurrentUserId();
            var fromUsername = Context.User.GetUserName();
            var fromConnectionId = Context.ConnectionId;

            await Clients.Client(toConnectionId).SendAsync("ReceiveOffer", new
            {
                FromConnectionId = fromConnectionId,
                FromUserId = fromUserId,
                FromUsername = fromUsername,
                Offer = offer
            });

            _logger.LogDebug($"Offer sent from {fromConnectionId} to {toConnectionId}");
        }

        /// <summary>
        /// Gửi WebRTC Answer đến peer cụ thể
        /// </summary>
        public async Task SendAnswer(string toConnectionId, object answer)
        {
            var fromUserId = Context.User.GetCurrentUserId();
            var fromUsername = Context.User.GetUserName();
            var fromConnectionId = Context.ConnectionId;

            await Clients.Client(toConnectionId).SendAsync("ReceiveAnswer", new
            {
                FromConnectionId = fromConnectionId,
                FromUserId = fromUserId,
                FromUsername = fromUsername,
                Answer = answer
            });

            _logger.LogDebug($"Answer sent from {fromConnectionId} to {toConnectionId}");
        }

        /// <summary>
        /// Gửi ICE Candidate đến peer cụ thể
        /// </summary>
        public async Task SendIceCandidate(string toConnectionId, object candidate)
        {
            var fromConnectionId = Context.ConnectionId;

            await Clients.Client(toConnectionId).SendAsync("ReceiveIceCandidate", new
            {
                FromConnectionId = fromConnectionId,
                Candidate = candidate
            });

            _logger.LogDebug($"ICE candidate sent from {fromConnectionId} to {toConnectionId}");
        }


        // ============================================
        // MEDIA CONTROLS (Optional - để notify UI)
        // ============================================

        /// <summary>
        /// Toggle camera (notify others)
        /// </summary>
        public async Task ToggleCamera(Guid roomId, bool isEnabled)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            await Clients.OthersInGroup(roomId.ToString()).SendAsync("CameraToggled", new
            {
                UserId = userId,
                Username = username,
                IsEnabled = isEnabled
            });
        }

        /// <summary>
        /// Toggle microphone (notify others)
        /// </summary>
        public async Task ToggleMicrophone(Guid roomId, bool isEnabled)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            await Clients.OthersInGroup(roomId.ToString()).SendAsync("MicrophoneToggled", new
            {
                UserId = userId,
                Username = username,
                IsEnabled = isEnabled
            });
        }

        /// <summary>
        /// Start/Stop screen sharing (notify others)
        /// </summary>
        public async Task ToggleScreenShare(Guid roomId, bool isSharing)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            await Clients.OthersInGroup(roomId.ToString()).SendAsync("ScreenShareToggled", new
            {
                UserId = userId,
                Username = username,
                IsSharing = isSharing
            });
        }


        // ============================================
        // CONNECTION LIFECYCLE
        // ============================================

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            _logger.LogInformation($"User {username} ({userId}) connected to MeetingHub: {Context.ConnectionId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User.GetCurrentUserId();
            var connectionId = Context.ConnectionId;

            // Tìm room mà user đang ở
            var roomId = _roomConnections
                .FirstOrDefault(kvp => kvp.Value.Any(c => c.ConnectionId == connectionId))
                .Key;

            if (roomId != Guid.Empty)
            {
                await RemoveFromRoom(roomId, connectionId, userId);

                // Update leave time trong DB
                await _joinRepo.LeaveMeetingAsync(roomId, userId);
            }

            _logger.LogInformation($"User {userId} disconnected from MeetingHub");

            await base.OnDisconnectedAsync(exception);
        }


        // ============================================
        // HELPERS
        // ============================================

        private async Task RemoveFromRoom(Guid roomId, string connectionId, Guid userId)
        {
            try
            {
                // Remove từ tracking
                if (_roomConnections.ContainsKey(roomId))
                {
                    _roomConnections[roomId].RemoveAll(c => c.ConnectionId == connectionId);

                    // Xóa room nếu không còn ai
                    if (_roomConnections[roomId].Count == 0)
                    {
                        _roomConnections.TryRemove(roomId, out _);
                    }
                }

                // Remove từ SignalR group
                await Groups.RemoveFromGroupAsync(connectionId, roomId.ToString());

                // Thông báo cho những người còn lại
                await Clients.OthersInGroup(roomId.ToString()).SendAsync("UserLeft", new
                {
                    ConnectionId = connectionId,
                    UserId = userId
                });

                _logger.LogInformation($"User {userId} left room {roomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing user from room {roomId}");
            }
        }
    }
}