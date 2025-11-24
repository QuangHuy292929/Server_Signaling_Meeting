using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServerSignaling_Meeting.Hubs
{
    [Authorize]
    public class MeetingHub : Hub
    {
        private readonly IRoomMeetingRepository _roomRepo;
        private readonly IJoinMeetingRepository _joinRepo;
        private readonly ILogger<MeetingHub> _logger;

        private static readonly ConcurrentDictionary<Guid, List<ConnectionInfo>> _waitingList = new();

        // ⭐ Tracking connections: roomId -> ConnectionId -> ConnectionInfo
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ConnectionInfo>> _roomConnections = new();

        // ⭐ Reverse lookup: ConnectionId -> RoomId (for cleanup on disconnect)
        private static readonly ConcurrentDictionary<string, Guid> _connectionToRoom = new();

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
            public bool CamEnabled { get; set; }
            public bool MicEnabled { get; set; }
            public bool IsHost { get; set; }
        }


        // ============================================
        // JOIN & LEAVE ROOM
        // ============================================

        /// <summary>
        /// Join vào room (client gọi sau khi đã join qua API)
        /// </summary>
        private async Task AddToActiveRoom(Guid roomId, ConnectionInfo info)
        {
            // 1. Add vào SignalR Group (Lúc này mới nhận được tin nhắn/video)
            await Groups.AddToGroupAsync(info.ConnectionId, roomId.ToString());

            // 2. Lưu vào danh sách chính thức (_roomConnections)
            var roomDict = _roomConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ConnectionInfo>());
            roomDict.TryAdd(info.ConnectionId, info);
            _connectionToRoom.TryAdd(info.ConnectionId, roomId);


            _logger.LogInformation($"[DEBUG] Connection Info : {info}");

            // 3. Gửi danh sách người đang họp cho người mới
            var existingParticipants = roomDict.Values
                .Where(c => c.ConnectionId != info.ConnectionId)
                .Select(c => new
                {
                    c.ConnectionId,
                    c.UserId,
                    c.Username,
                    c.CamEnabled,
                    c.MicEnabled
                })
                .ToList();

            await Clients.Client(info.ConnectionId).SendAsync("ExistingParticipants", existingParticipants);

            _logger.LogInformation($"[DEBUG] ExistingParticipants: {existingParticipants}");

            // 4. Thông báo cho cả phòng là có người mới vào
            await Clients.OthersInGroup(roomId.ToString()).SendAsync("UserJoined", new
            {
                // ... map info ...
                UserId = info.UserId,
                Username = info.Username,
                ConnectionId = info.ConnectionId,
                JoinedAt = DateTime.UtcNow,
                CamEnable = info.CamEnabled,
                MicEnable = info.MicEnabled
            });
        }
        public async Task JoinRoom(Guid roomId, bool camEnable, bool micEnable)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            try
            { 
                // Kiểm tra quyền join
                var joinResult = await _joinRepo.GetParticipantByUserAndRoomAsync(userId, roomId);

                _logger.LogInformation($"[DEBUG] user ${userId} in room {roomId} info: {joinResult} ");
                if (joinResult == null)
                {
                    await Clients.Caller.SendAsync("Error", "You must join room via API first");
                    return;
                }
                
                    //check role
                bool isHost = joinResult.Role == "host";

                var joinInfo = new ConnectionInfo
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    Username = username,
                    CamEnabled = camEnable,
                    MicEnabled = micEnable,
                    IsHost = isHost,
                    JoinedAt = DateTime.UtcNow //tạm thời cho hiện tại, sau sửa lại khi join thành công mới update
                };

                if (isHost)
                {
                    await AddToActiveRoom(roomId, joinInfo);
                    if (_waitingList.TryGetValue(roomId, out var waitingGuests) && waitingGuests.Count > 0)
                    {
                        // Gửi từng người hoặc gửi cả list cho Host
                        foreach (var guest in waitingGuests)
                        {
                            await Clients.Client(connectionId).SendAsync("GuestRequested", new
                            {
                                UserId = guest.UserId,
                                Username = guest.Username,
                                ConnectionId = guest.ConnectionId
                            });
                        }
                    }
                }
                else
                {

                    // Nếu trạng thái trong DB là 'Rejected' -> Chặn luôn
                    if (joinResult.status == "rejected")
                    {
                        await Clients.Caller.SendAsync("YouAreRejected");
                        return;
                    }

                    // Nếu trạng thái là 'Joined' (ví dụ rớt mạng vào lại) -> Cho vào luôn
                    if (joinResult.status == "joined")
                    {
                        await AddToActiveRoom(roomId, joinInfo);
                        return;
                    }

                    if (joinResult.status == "pending")
                    {
                        var waitingGuests = _waitingList.GetOrAdd(roomId, _ => new List<ConnectionInfo>());
                        lock (waitingGuests)
                        {
                            if (!waitingGuests.Any(x => x.ConnectionId == connectionId))
                            {
                                waitingGuests.Add(joinInfo);
                            }
                        }

                        // Báo cho Guest: "Bạn đang chờ"
                        await Clients.Caller.SendAsync("YouAreWaiting");

                        // Báo cho Host (Tìm kết nối của Host trong phòng active)
                        var activeRoom = _roomConnections.GetValueOrDefault(roomId);
                        if (activeRoom != null)
                        {
                            // Giả sử ta lọc ra ông nào là Host đang online
                            var hostConnection = activeRoom.Values.FirstOrDefault(x => x.IsHost);

                            if (hostConnection != null)
                            {
                                await Clients.Client(hostConnection.ConnectionId).SendAsync("GuestRequested", new
                                {
                                    Username = joinInfo.Username,
                                    UserId = joinInfo.UserId,
                                    ConnectionId = connectionId
                                });

                                _logger.LogInformation($"[DEBUG] GuestRequested {joinInfo.Username} {joinInfo.UserId} {connectionId} to hostconnetion: {hostConnection.ConnectionId}");
                            }
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining room {roomId}");
                await Clients.Caller.SendAsync("Error", "Failed to join room");
            }
        }

        public async Task AdmitUser(string guestConnectionId)
        {
            var hostConnectionId = Context.ConnectionId;

            // 1. Tự tìm RoomId dựa trên Host (người gọi hàm)
            // Nếu Host không ở trong phòng nào -> Lỗi
            if (!_connectionToRoom.TryGetValue(hostConnectionId, out var roomId))
            {
                return;
            }

            // 2. Lấy danh sách chờ CỦA PHÒNG ĐÓ
            if (_waitingList.TryGetValue(roomId, out var waitingGuests))
            {
                ConnectionInfo guestInfo;
                lock (waitingGuests)
                {
                    guestInfo = waitingGuests.FirstOrDefault(x => x.ConnectionId == guestConnectionId);
                    if (guestInfo != null)
                    {
                        waitingGuests.Remove(guestInfo);
                    }
                }

                if (guestInfo != null)
                {

                    await Groups.AddToGroupAsync(guestInfo.ConnectionId, roomId.ToString());

                    // 2. Lưu vào danh sách chính thức (_roomConnections)
                    var roomDict = _roomConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ConnectionInfo>());
                    roomDict.TryAdd(guestInfo.ConnectionId, guestInfo);
                    _connectionToRoom.TryAdd(guestInfo.ConnectionId, roomId);


                    _logger.LogInformation($"[DEBUG] Connection Info : {guestInfo}");

                    // 3. Gửi danh sách người đang họp cho người mới
                    var existingParticipants = roomDict.Values
                        .Where(c => c.ConnectionId != guestInfo.ConnectionId)
                        .Select(c => new
                        {
                            c.ConnectionId,
                            c.UserId,
                            c.Username,
                            c.CamEnabled,
                            c.MicEnabled
                        })
                        .ToList();

                    await Clients.Client(guestInfo.ConnectionId).SendAsync("ExistingParticipants", existingParticipants);


                    var joinInfo = await _joinRepo.GetParticipantByUserAndRoomAsync(guestInfo.UserId, roomId);

                    // 3. Cập nhật DB & Chuyển vào phòng
                    await _joinRepo.UpdateStatusAsync(joinInfo.Id, roomId, guestInfo.UserId, "joined", DateTime.Now);

                    await Clients.GroupExcept(roomId.ToString(), guestInfo.UserId.ToString()).SendAsync("UserJoined", new
                    {
                        // ... map info ...
                        UserId = guestInfo.UserId,
                        Username = guestInfo.Username,
                        ConnectionId = guestInfo.ConnectionId,
                        JoinedAt = DateTime.UtcNow,
                        CamEnable = guestInfo.CamEnabled,
                        MicEnable = guestInfo.MicEnabled
                    });
                }
            }
        }

        public async Task RejectUser(string guestConnectionId)
        {
            var hostConnectionId = Context.ConnectionId;

            // 1. Tự tìm RoomId từ Host
            if (!_connectionToRoom.TryGetValue(hostConnectionId, out var roomId))
            {
                return;
            }

            if (_waitingList.TryGetValue(roomId, out var waitingGuests))
            {
                ConnectionInfo guestInfo = null;
                lock (waitingGuests)
                {
                    guestInfo = waitingGuests.FirstOrDefault(x => x.ConnectionId == guestConnectionId);
                    if (guestInfo != null)
                    {
                        waitingGuests.Remove(guestInfo);
                    }
                }

                if (guestInfo != null)
                {
                    var joinInfo = await _joinRepo.GetParticipantByUserAndRoomAsync(guestInfo.UserId, roomId);
                    await _joinRepo.UpdateStatusAsync(joinInfo.Id, roomId, guestInfo.UserId, "rejected", null);
                    await Clients.Client(guestConnectionId).SendAsync("YouAreRejected");
                }
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

            // Validation: Check if target connection exists
            if (!_connectionToRoom.ContainsKey(toConnectionId))
            {
                await Clients.Caller.SendAsync("Error", "Target user not found or not in any room");
                _logger.LogWarning($"SendOffer failed: Target {toConnectionId} not found");
                return;
            }

            // Optional: Check if both users are in the same room
            if (_connectionToRoom.TryGetValue(fromConnectionId, out var fromRoom) &&
                _connectionToRoom.TryGetValue(toConnectionId, out var toRoom))
            {
                if (fromRoom != toRoom)
                {
                    await Clients.Caller.SendAsync("Error", "Target user is in a different room");
                    _logger.LogWarning($"SendOffer failed: Users in different rooms");
                    return;
                }
            }

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

            // Validation: Check if target connection exists
            if (!_connectionToRoom.ContainsKey(toConnectionId))
            {
                await Clients.Caller.SendAsync("Error", "Target user not found or not in any room");
                _logger.LogWarning($"SendAnswer failed: Target {toConnectionId} not found");
                return;
            }

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

            // Validation: Check if target connection exists
            if (!_connectionToRoom.ContainsKey(toConnectionId))
            {
                await Clients.Caller.SendAsync("Error", "Target user not found or not in any room");
                _logger.LogWarning($"SendIceCandidate failed: Target {toConnectionId} not found");
                return;
            }

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
        public async Task ToggleCamera(bool isEnabled)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            // Find user's room
            if (!_connectionToRoom.TryGetValue(connectionId, out var roomId))
            {
                await Clients.Caller.SendAsync("Error", "You are not in any room");
                return;
            }

            var roomDict = _roomConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ConnectionInfo>());
            if (roomDict.TryGetValue(connectionId, out var connInfo))
            {
                connInfo.CamEnabled = isEnabled; // Cập nhật vào RAM server
            }

            _logger.LogInformation($"[DEBUG] User {username} ({userId}) in room {roomId} toggled camera to {(isEnabled ? "ON" : "OFF")}");

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
        public async Task ToggleMicrophone(bool isEnabled)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            // Find user's room
            if (!_connectionToRoom.TryGetValue(connectionId, out var roomId))
            {
                await Clients.Caller.SendAsync("Error", "You are not in any room");
                return;
            }

            var roomDict = _roomConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ConnectionInfo>());
            if (roomDict.TryGetValue(connectionId, out var connInfo))
            {
                connInfo.MicEnabled = isEnabled; // Cập nhật vào RAM server
            }

            _logger.LogInformation($"[DEBUG] User {username} ({userId}) in room {roomId} toggled microphone to {(isEnabled ? "ON" : "OFF")}");

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
        public async Task ToggleScreenShare(bool isSharing)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            // Find user's room
            if (!_connectionToRoom.TryGetValue(connectionId, out var roomId))
            {
                await Clients.Caller.SendAsync("Error", "You are not in any room");
                return;
            }

            _logger.LogInformation($"[DEBUG] user {username} ToggleScreenShare: {isSharing}");

            await Clients.OthersInGroup(roomId.ToString()).SendAsync("ScreenShareToggled", new
            {
                UserId = userId,
                Username = username,
                IsEnabled = isSharing
            });
        }

        //Chat in meeting
        public async Task SendChatMessage(string content)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            if (_connectionToRoom.TryGetValue(connectionId, out var roomId))
            {
                // Tạo gói tin đầy đủ thông tin
                var messageData = new
                {
                    UserId = userId,
                    Username = username,
                    Content = content,
                    Timestamp = DateTime.UtcNow
                };

                // Gửi cho tất cả mọi người trong Group (Room)
                await Clients.Group(roomId.ToString()).SendAsync("ReceiveChatMessage", messageData);
            }
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

            // Tìm room mà user đang ở - sử dụng reverse lookup
            if (_connectionToRoom.TryGetValue(connectionId, out var roomId))
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

        // MeetingHub.cs

        private async Task RemoveFromRoom(Guid roomId, string connectionId, Guid userId)
        {
            bool isHostLeaving = false;

            // Lấy thông tin phòng
            if (_roomConnections.TryGetValue(roomId, out var roomDict))
            {
                if (roomDict.TryGetValue(connectionId, out var info))
                {
                    isHostLeaving = info.IsHost;
                }

                // Xóa người gọi (Host/Guest) khỏi danh sách RAM
                roomDict.TryRemove(connectionId, out _);
                _connectionToRoom.TryRemove(connectionId, out _);
                await Groups.RemoveFromGroupAsync(connectionId, roomId.ToString());

                // LOGIC QUAN TRỌNG: NẾU HOST RỜI ĐI
                if (isHostLeaving)
                {
                    _logger.LogInformation($"Host {userId} ended meeting {roomId}. Force updating LeftAt for all participants.");

                    // Lấy danh sách tất cả những người còn lại trong phòng
                    var remainingParticipants = roomDict.Values.ToList();

                    // Cập nhật Database cho TẤT CẢ mọi người (bao gồm cả Host và những người còn lại)
                    // Cập nhật cho Host (người vừa rời)
                    await _joinRepo.LeaveMeetingAsync(roomId, userId);

                    // Cập nhật cho những người còn lại (Force Leave)
                    foreach (var participant in remainingParticipants)
                    {
                        // Cập nhật DB: Set LeftAt = Now, Status = Ended
                        await _joinRepo.LeaveMeetingAsync(roomId, participant.UserId);

                        // Xóa mapping của họ trong RAM luôn (để khi họ disconnect thật sự, OnDisconnectedAsync không làm gì nữa)
                        _connectionToRoom.TryRemove(participant.ConnectionId, out _);
                    }

                    //  Gửi tín hiệu giải tán
                    await Clients.OthersInGroup(roomId.ToString()).SendAsync("MeetingEnded", "Người chủ trì đã kết thúc cuộc họp.");

                    //  Xóa sạch phòng khỏi RAM
                    _roomConnections.TryRemove(roomId, out _);
                    _waitingList.TryRemove(roomId, out _);
                }
                else
                {
                    //Trường hợp 2 người dùng chủ động tắt
                    // Nếu đây là chủ động gọi LeaveRoom (bấm nút), ta update DB luôn
                    await _joinRepo.LeaveMeetingAsync(roomId, userId);

                    // Xóa phòng nếu không còn ai
                    if (roomDict.IsEmpty)
                    {
                        _roomConnections.TryRemove(roomId, out _);
                    }

                    await Clients.OthersInGroup(roomId.ToString()).SendAsync("UserLeft", new
                    {
                        ConnectionId = connectionId,
                        UserId = userId
                    });
                }
            }
        }
    }
}