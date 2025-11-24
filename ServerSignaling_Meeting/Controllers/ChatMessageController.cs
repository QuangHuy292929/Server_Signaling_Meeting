using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ServerSignaling_Meeting.Dtos.ChatDto;
using ServerSignaling_Meeting.Dtos.Message;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Hubs;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;
using System.Security.Claims;


namespace ServerSignaling_Meeting.Controllers
{
    [Route("api/message")]
    [ApiController]
    [Authorize]
    public class ChatMessageController : ControllerBase
    {
        private readonly IChatMessageRepository _messageRepo;
        private readonly IJoinGroupRepository _joinGroupRepo;
        private readonly ILogger<ChatMessageController> _logger;
        private readonly IHubContext<ChatHub> _hubContext; // ✅ THÊM DÒNG NÀY


        public ChatMessageController(
            IChatMessageRepository messageRepo,
            IJoinGroupRepository joinGroupRepo,
            ILogger<ChatMessageController> logger,
             IHubContext<ChatHub> hubContext)
        {
            _messageRepo = messageRepo;
            _joinGroupRepo = joinGroupRepo;
            _logger = logger;
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext)); // ✅ THROW NẾU NULL

        }

        [HttpPost("groups/{groupId}/send")]
        public async Task<IActionResult> SendMessage(Guid groupId, [FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = User.GetCurrentUserId();
                // var username = User.GetUserName(); //  LẤY USERNAME
                //  LẤY USERNAME TỪ CLAIMS
                var username = User.FindFirst(ClaimTypes.Name)?.Value
                            ?? User.FindFirst("username")?.Value
                            ?? User.Identity?.Name
                            ?? "Unknown";

                _logger.LogInformation($"SendMessage called by user {userId} ({username}) to group {groupId}");

                // Kiểm tra user có trong group không
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                {
                    _logger.LogWarning($"User {userId} is not member of group {groupId}");
                    return Forbid();
                }

                // Tạo entity ChatMessage
                var message = new ChatMessage
                {
                    GroupId = groupId,
                    UserId = userId,
                    TypeMessage = request.TypeMessage,
                    ContentMessage = request.Content,
                    FileName = request.FileName,
                    FileUrl = request.FileUrl
                };

                // Lưu message
                var savedMessage = await _messageRepo.SaveMessageAsync(message);

                // GỬI QUA SIGNALR 
                await _hubContext.Clients.Group(groupId.ToString())
                    .SendAsync("ReceiveGroupMessage", new
                    {
                        MessageId = savedMessage.Id,
                        GroupId = groupId,
                        UserId = userId,
                        Username = username, 
                        Content = request.Content,
                        TypeMessage = request.TypeMessage,
                        FileName = request.FileName,    
                        FileUrl = request.FileUrl,
                        SendAt = DateTime.UtcNow
                    });

                _logger.LogInformation($" Sent SignalR message to group {groupId}");

                // Response
                return Ok(new
                {
                    success = true,
                    data = savedMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SendMessage - GroupId: {groupId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }




        // GET: api/messages/groups/{groupId}
        [HttpGet("groups/{groupId}")]
        public async Task<IActionResult> GetGroupMessages(
            Guid groupId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                _logger.LogInformation($"GetGroupMessages called - GroupId: {groupId}, Page: {page}");

                var userId = User.GetCurrentUserId();

                // 1. Check quyền thành viên
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);
                if (!isMember)
                {
                    return Forbid();
                }

                // 2. Lấy dữ liệu từ Repo
                var skip = (page - 1) * pageSize;
                var messages = await _messageRepo.GetMessagesByGroupIdAsync(groupId, pageSize, skip);
                var totalCount = await _messageRepo.GetGroupMessageCountAsync(groupId);

                // 3. [QUAN TRỌNG] Map từ Entity sang Anonymous Object (DTO)
                // Việc này giúp cắt đứt vòng lặp JSON và đổi tên trường cho khớp Client
                var data = messages.Select(m => new
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    UserId = m.UserId,
                    Username = m.User?.UserName ?? "Unknown", // Lấy tên người gửi

                    // Avatar = null, // BỎ AVATAR NHƯ YÊU CẦU

                    Content = m.ContentMessage, // Map: DB là ContentMessage -> API trả về Content
                    TypeMessage = m.TypeMessage,
                    FileName = m.FileName,
                    FileUrl = m.FileUrl,
                    SendAt = m.SendAt,

                    // Server tính luôn tin này có phải của người đang gọi API không
                    IsMyMessage = m.UserId == userId
                }).ToList();

                // (Tùy chọn) Nếu Client cần hiển thị từ cũ đến mới thì Reverse lại
                // data.Reverse(); 

                var response = new
                {
                    success = true,
                    data = data, // Trả về dữ liệu đã map
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasMore = skip + messages.Count() < totalCount
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetGroupMessages: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        // GET: api/messages/{messageId}
        [HttpGet("{messageId}")]
        public async Task<IActionResult> GetMessageById(Guid messageId)
        {
            try
            {
                _logger.LogInformation($"GetMessageById called - MessageId: {messageId}");

                var message = await _messageRepo.GetMessageByIdAsync(messageId);

                if (message == null)
                {
                    _logger.LogWarning($"Message not found: {messageId}");
                    return NotFound();
                }

                var userId = User.GetCurrentUserId();
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, message.GroupId);

                if (!isMember)
                {
                    _logger.LogWarning($"User {userId} is not member of group {message.GroupId}");
                    return Forbid();
                }

                return Ok(new { success = true, data = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetMessageById - MessageId: {messageId}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // PUT: api/messages/{messageId}
        [HttpPut("{messageId}")]
        public async Task<IActionResult> UpdateMessage(
            Guid messageId,
            [FromBody] UpdateMessageRequest request)
        {
            try
            {
                _logger.LogInformation($"UpdateMessage called - MessageId: {messageId}, Content: {request.Content}");

                var message = await _messageRepo.GetMessageByIdAsync(messageId);

                if (message == null)
                {
                    _logger.LogWarning($"Message not found: {messageId}");
                    return NotFound();
                }

                var userId = User.GetCurrentUserId();

                if (message.UserId != userId)
                {
                    _logger.LogWarning($"User {userId} is not owner of message {messageId}");
                    return Forbid();
                }

                await _messageRepo.UpdateMessageAsync(messageId, request.Content);

                var updatedMessage = await _messageRepo.GetMessageByIdAsync(messageId);
                _logger.LogInformation($"Message updated successfully: {messageId}");

                return Ok(new { success = true, data = updatedMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UpdateMessage - MessageId: {messageId}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE: api/messages/{messageId}
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            try
            {
                _logger.LogInformation($"DeleteMessage called - MessageId: {messageId}");

                var message = await _messageRepo.GetMessageByIdAsync(messageId);

                if (message == null)
                {
                    _logger.LogWarning($"Message not found: {messageId}");
                    return NotFound();
                }

                var userId = User.GetCurrentUserId();
                var member = await _joinGroupRepo.GetMemberByUserAndGroupAsync(userId, message.GroupId);

                if (message.UserId != userId && member?.Role != "owner" && member?.Role != "admin")
                {
                    _logger.LogWarning($"User {userId} has no permission to delete message {messageId}");
                    return Forbid();
                }

                await _messageRepo.DeleteMessageAsync(messageId);
                _logger.LogInformation($"Message deleted successfully: {messageId}");

                return Ok(new { success = true, message = "Message deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeleteMessage - MessageId: {messageId}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/messages/groups/{groupId}/search
        [HttpGet("groups/{groupId}/search")]
        public async Task<IActionResult> SearchMessages(
            Guid groupId,
            [FromQuery] string keyword)
        {
            try
            {
                _logger.LogInformation($"SearchMessages called - GroupId: {groupId}, Keyword: {keyword}");

                var userId = User.GetCurrentUserId();
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);

                if (!isMember)
                {
                    _logger.LogWarning($"User {userId} is not member of group {groupId}");
                    return Forbid();
                }

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    _logger.LogWarning("Keyword is empty");
                    return BadRequest(new { success = false, message = "Keyword is required" });
                }

                var messages = await _messageRepo.SearchMessagesAsync(groupId, keyword);
                _logger.LogInformation($"Found {messages.Count()} messages");

                return Ok(new { success = true, data = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SearchMessages - GroupId: {groupId}, Keyword: {keyword}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/messages/groups/{groupId}/type/{typeMessage}
        [HttpGet("groups/{groupId}/type/{typeMessage}")]
        public async Task<IActionResult> GetMessagesByType(Guid groupId, string typeMessage)
        {
            try
            {
                _logger.LogInformation($"GetMessagesByType called - GroupId: {groupId}, Type: {typeMessage}");

                var userId = User.GetCurrentUserId();
                var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);

                if (!isMember)
                {
                    _logger.LogWarning($"User {userId} is not member of group {groupId}");
                    return Forbid();
                }

                var messages = await _messageRepo.GetMessagesByTypeAsync(groupId, typeMessage);
                _logger.LogInformation($"Found {messages.Count()} messages of type {typeMessage}");

                return Ok(new { success = true, data = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetMessagesByType - GroupId: {groupId}, Type: {typeMessage}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/messages/my-messages
        [HttpGet("my-messages")]
        public async Task<IActionResult> GetMyMessages()
        {
            try
            {
                var userId = User.GetCurrentUserId();
                _logger.LogInformation($"GetMyMessages called - UserId: {userId}");

                var messages = await _messageRepo.GetMessagesByUserIdAsync(userId);
                _logger.LogInformation($"Found {messages.Count()} messages");

                return Ok(new { success = true, data = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMyMessages");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}