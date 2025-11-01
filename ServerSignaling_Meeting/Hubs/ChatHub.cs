// Hubs/ChatHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;
using System.Collections.Concurrent;

namespace ServerSignaling_Meeting.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatMessageRepository _messageRepo;
        private readonly IGroupChatRepository _groupRepo;
        private readonly IJoinGroupRepository _joinGroupRepo;
        private readonly ILogger<ChatHub> _logger;

        // ⭐ Tracking online users: userId -> List<connectionIds>
        private static readonly ConcurrentDictionary<Guid, List<string>> _userConnections = new();

        // ⭐ Tracking user info: connectionId -> UserInfo
        private static readonly ConcurrentDictionary<string, UserInfo> _connectionInfo = new();

        public ChatHub(
            IChatMessageRepository messageRepo,
            IGroupChatRepository groupRepo,
            IJoinGroupRepository joinGroupRepo,
            ILogger<ChatHub> logger)
        {
            _messageRepo = messageRepo;
            _groupRepo = groupRepo;
            _joinGroupRepo = joinGroupRepo;
            _logger = logger;
        }

        public class UserInfo
        {
            public Guid UserId { get; set; }
            public string Username { get; set; }
            public string Email { get; set; }
            public DateTime ConnectedAt { get; set; }
        }


        // ============================================
        // CONNECTION LIFECYCLE
        // ============================================

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var email = Context.User.GetEmail();
            var connectionId = Context.ConnectionId;

            try
            {
                // Track connection
                if (!_userConnections.ContainsKey(userId))
                {
                    _userConnections[userId] = new List<string>();
                }
                _userConnections[userId].Add(connectionId);

                _connectionInfo[connectionId] = new UserInfo
                {
                    UserId = userId,
                    Username = username,
                    Email = email,
                    ConnectedAt = DateTime.UtcNow
                };

                // Auto-join vào tất cả groups của user
                var userGroups = await _joinGroupRepo.GetGroupsByUserIdAsync(userId);
                foreach (var joinGroup in userGroups)
                {
                    if (joinGroup.Status == "JOIN" && joinGroup.GroupChat != null)
                    {
                        await Groups.AddToGroupAsync(connectionId, joinGroup.GroupId.ToString());
                        _logger.LogDebug($"User {username} auto-joined group {joinGroup.GroupChat.GroupName}");
                    }
                }

                // Thông báo user online (chỉ lần đầu tiên)
                if (_userConnections[userId].Count == 1)
                {
                    await Clients.Others.SendAsync("UserOnline", new
                    {
                        UserId = userId,
                        Username = username,
                        Timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogInformation($"User {username} ({userId}) connected to ChatHub. Total connections: {_userConnections[userId].Count}");

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnConnectedAsync for user {userId}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;

            try
            {
                if (_connectionInfo.TryGetValue(connectionId, out var userInfo))
                {
                    var userId = userInfo.UserId;

                    // Remove connection
                    if (_userConnections.ContainsKey(userId))
                    {
                        _userConnections[userId].Remove(connectionId);

                        // User offline hoàn toàn
                        if (_userConnections[userId].Count == 0)
                        {
                            _userConnections.TryRemove(userId, out _);

                            await Clients.Others.SendAsync("UserOffline", new
                            {
                                UserId = userId,
                                Timestamp = DateTime.UtcNow
                            });

                            _logger.LogInformation($"User {userInfo.Username} ({userId}) is now offline");
                        }
                        else
                        {
                            _logger.LogInformation($"User {userInfo.Username} ({userId}) disconnected. Remaining connections: {_userConnections[userId].Count}");
                        }
                    }

                    _connectionInfo.TryRemove(connectionId, out _);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
            }
        }


        // ============================================
        // SEND MESSAGES
        // ============================================

        /// <summary>
        /// Gửi tin nhắn đến nhóm
        /// </summary>
        public async Task SendGroupMessage(Guid groupId, string message, string typeMessage = "TEXT")
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(message))
                {
                    await Clients.Caller.SendAsync("Error", "Message cannot be empty");
                    return;
                }

                // Kiểm tra user có trong group không
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a member of this group");
                    return;
                }

                // Kiểm tra group có active không
                var group = await _groupRepo.GetGroupByIdAsync(groupId);
                if (group == null || group.Status != "ACTIVE")
                {
                    await Clients.Caller.SendAsync("Error", "Group is not active");
                    return;
                }

                // Lưu tin nhắn vào database
                var savedMessage = await _messageRepo.SaveMessageAsync(new ChatMessage
                {
                    GroupId = groupId,
                    UserId = userId,
                    ContentMessage = message,
                    TypeMessage = typeMessage
                });

                // Gửi real-time đến tất cả thành viên trong group
                await Clients.Group(groupId.ToString()).SendAsync("ReceiveGroupMessage", new
                {
                    MessageId = savedMessage.Id,
                    GroupId = groupId,
                    UserId = userId,
                    Username = username,
                    Content = message,
                    TypeMessage = typeMessage,
                    SendAt = savedMessage.SendAt
                });

                _logger.LogInformation($"Message sent to group {groupId} by user {username}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message to group {groupId}");
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        /// <summary>
        /// Gửi tin nhắn riêng đến user cụ thể
        /// </summary>
        public async Task SendPrivateMessage(Guid toUserId, string message)
        {
            var fromUserId = Context.User.GetCurrentUserId();
            var fromUsername = Context.User.GetUserName();

            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    await Clients.Caller.SendAsync("Error", "Message cannot be empty");
                    return;
                }

                // Không thể gửi cho chính mình
                if (fromUserId == toUserId)
                {
                    await Clients.Caller.SendAsync("Error", "Cannot send message to yourself");
                    return;
                }

                // Lưu tin nhắn private (optional - tùy requirements)
                var savedMessage = await _messageRepo.SaveMessageAsync(new ChatMessage
                {
                    UserId = fromUserId,
                    ContentMessage = message,
                    TypeMessage = "PRIVATE"
                });

                // Kiểm tra user có online không
                if (_userConnections.TryGetValue(toUserId, out var toUserConnections))
                {
                    // Gửi đến TẤT CẢ devices của người nhận
                    await Clients.Clients(toUserConnections).SendAsync("ReceivePrivateMessage", new
                    {
                        MessageId = savedMessage.Id,
                        FromUserId = fromUserId,
                        FromUsername = fromUsername,
                        Content = message,
                        SendAt = savedMessage.SendAt
                    });

                    // Confirmation cho người gửi (tất cả devices)
                    if (_userConnections.TryGetValue(fromUserId, out var fromUserConnections))
                    {
                        await Clients.Clients(fromUserConnections).SendAsync("MessageSent", new
                        {
                            MessageId = savedMessage.Id,
                            ToUserId = toUserId,
                            Content = message,
                            SendAt = savedMessage.SendAt,
                            Status = "delivered"
                        });
                    }

                    _logger.LogInformation($"Private message sent from {fromUsername} to user {toUserId}");
                }
                else
                {
                    // User offline
                    await Clients.Caller.SendAsync("MessageSent", new
                    {
                        MessageId = savedMessage.Id,
                        ToUserId = toUserId,
                        Content = message,
                        SendAt = savedMessage.SendAt,
                        Status = "offline"
                    });

                    _logger.LogInformation($"Private message saved for offline user {toUserId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending private message to user {toUserId}");
                await Clients.Caller.SendAsync("Error", "Failed to send private message");
            }
        }


        // ============================================
        // TYPING INDICATOR
        // ============================================

        /// <summary>
        /// Thông báo đang typing trong group
        /// </summary>
        public async Task UserTyping(Guid groupId)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            try
            {
                // Kiểm tra quyền
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                    return;

                // Lấy tất cả connections của user này
                var myConnections = _userConnections.GetValueOrDefault(userId, new List<string>());

                // Gửi đến group nhưng loại trừ TẤT CẢ connections của chính mình
                await Clients.GroupExcept(groupId.ToString(), myConnections)
                    .SendAsync("UserTyping", new
                    {
                        GroupId = groupId,
                        UserId = userId,
                        Username = username,
                        Timestamp = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UserTyping for group {groupId}");
            }
        }

        /// <summary>
        /// Thông báo dừng typing
        /// </summary>
        public async Task UserStopTyping(Guid groupId)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();

            try
            {
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                    return;

                var myConnections = _userConnections.GetValueOrDefault(userId, new List<string>());

                await Clients.GroupExcept(groupId.ToString(), myConnections)
                    .SendAsync("UserStopTyping", new
                    {
                        GroupId = groupId,
                        UserId = userId,
                        Username = username,
                        Timestamp = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UserStopTyping for group {groupId}");
            }
        }


        // ============================================
        // GROUP MANAGEMENT
        // ============================================

        /// <summary>
        /// Join vào group (khi được thêm vào group mới)
        /// </summary>
        public async Task JoinGroup(Guid groupId)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            try
            {
                // Kiểm tra quyền
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a member of this group");
                    return;
                }

                // Add connection vào SignalR group
                await Groups.AddToGroupAsync(connectionId, groupId.ToString());

                // Thông báo cho group
                await Clients.Group(groupId.ToString()).SendAsync("UserJoinedGroup", new
                {
                    GroupId = groupId,
                    UserId = userId,
                    Username = username,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"User {username} joined group {groupId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining group {groupId}");
                await Clients.Caller.SendAsync("Error", "Failed to join group");
            }
        }

        /// <summary>
        /// Leave group
        /// </summary>
        public async Task LeaveGroup(Guid groupId)
        {
            var userId = Context.User.GetCurrentUserId();
            var username = Context.User.GetUserName();
            var connectionId = Context.ConnectionId;

            try
            {
                // Remove từ SignalR group
                await Groups.RemoveFromGroupAsync(connectionId, groupId.ToString());

                // Thông báo cho group
                await Clients.Group(groupId.ToString()).SendAsync("UserLeftGroup", new
                {
                    GroupId = groupId,
                    UserId = userId,
                    Username = username,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"User {username} left group {groupId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leaving group {groupId}");
            }
        }


        // ============================================
        // MESSAGE ACTIONS
        // ============================================

        /// <summary>
        /// Xóa tin nhắn
        /// </summary>
        public async Task DeleteMessage(Guid messageId, Guid groupId)
        {
            var userId = Context.User.GetCurrentUserId();

            try
            {
                var message = await _messageRepo.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found");
                    return;
                }

                // Kiểm tra quyền: chỉ người gửi hoặc admin/owner mới xóa được
                var member = await _joinGroupRepo.GetMemberByUserAndGroupAsync(userId, groupId);
                if (message.UserId != userId && member?.Role != "owner" && member?.Role != "admin")
                {
                    await Clients.Caller.SendAsync("Error", "You don't have permission to delete this message");
                    return;
                }

                // Xóa tin nhắn
                await _messageRepo.DeleteMessageAsync(messageId);

                // Thông báo cho group
                await Clients.Group(groupId.ToString()).SendAsync("MessageDeleted", new
                {
                    MessageId = messageId,
                    GroupId = groupId,
                    DeletedBy = userId,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"Message {messageId} deleted by user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting message {messageId}");
                await Clients.Caller.SendAsync("Error", "Failed to delete message");
            }
        }

        /// <summary>
        /// Sửa tin nhắn
        /// </summary>
        public async Task UpdateMessage(Guid messageId, Guid groupId, string newContent)
        {
            var userId = Context.User.GetCurrentUserId();

            try
            {
                if (string.IsNullOrWhiteSpace(newContent))
                {
                    await Clients.Caller.SendAsync("Error", "Message cannot be empty");
                    return;
                }

                var message = await _messageRepo.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found");
                    return;
                }

                // Chỉ người gửi mới sửa được
                if (message.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "You can only edit your own messages");
                    return;
                }

                // Update tin nhắn
                await _messageRepo.UpdateMessageAsync(messageId, newContent);

                // Thông báo cho group
                await Clients.Group(groupId.ToString()).SendAsync("MessageUpdated", new
                {
                    MessageId = messageId,
                    GroupId = groupId,
                    NewContent = newContent,
                    UpdatedBy = userId,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"Message {messageId} updated by user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating message {messageId}");
                await Clients.Caller.SendAsync("Error", "Failed to update message");
            }
        }


        // ============================================
        // UTILITY METHODS
        // ============================================

        /// <summary>
        /// Lấy danh sách users online
        /// </summary>
        public async Task GetOnlineUsers()
        {
            try
            {
                var onlineUsers = _userConnections.Keys.Select(userId =>
                {
                    var firstConnection = _userConnections[userId].FirstOrDefault();
                    if (firstConnection != null && _connectionInfo.TryGetValue(firstConnection, out var info))
                    {
                        return new
                        {
                            UserId = userId,
                            Username = info.Username,
                            ConnectionCount = _userConnections[userId].Count
                        };
                    }
                    return null;
                }).Where(u => u != null).ToList();

                await Clients.Caller.SendAsync("OnlineUsersList", new
                {
                    Users = onlineUsers,
                    TotalCount = onlineUsers.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
            }
        }

        /// <summary>
        /// Check user có online không
        /// </summary>
        public async Task CheckUserOnline(Guid userId)
        {
            try
            {
                var isOnline = _userConnections.ContainsKey(userId);
                await Clients.Caller.SendAsync("UserOnlineStatus", new
                {
                    UserId = userId,
                    IsOnline = isOnline,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking online status for user {userId}");
            }
        }
    }
}