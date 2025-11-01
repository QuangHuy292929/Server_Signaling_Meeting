using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Dtos.Message;

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

        public ChatMessageController(
            IChatMessageRepository messageRepo,
            IJoinGroupRepository joinGroupRepo,
            ILogger<ChatMessageController> logger)
        {
            _messageRepo = messageRepo;
            _joinGroupRepo = joinGroupRepo;
            _logger = logger;
        }

        // GET: api/messages/groups/{groupId}
        [HttpGet("groups/{groupId}")]
        public async Task<IActionResult> GetGroupMessages(
            Guid groupId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userId = User.GetCurrentUserId();
            var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);

            if (!isMember)
                return Forbid();

            var skip = (page - 1) * pageSize;
            var messages = await _messageRepo.GetMessagesByGroupIdAsync(groupId, pageSize, skip);
            var totalCount = await _messageRepo.GetGroupMessageCountAsync(groupId);

            return Ok(new
            {
                success = true,
                data = messages,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    hasMore = skip + messages.Count() < totalCount
                }
            });
        }

        // GET: api/messages/{messageId}
        [HttpGet("{messageId}")]
        public async Task<IActionResult> GetMessageById(Guid messageId)
        {
            var message = await _messageRepo.GetMessageByIdAsync(messageId);

            if (message == null)
                return NotFound();

            var userId = User.GetCurrentUserId();
            var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, message.GroupId);

            if (!isMember)
                return Forbid();

            return Ok(new { success = true, data = message });
        }

        // PUT: api/messages/{messageId}
        [HttpPut("{messageId}")]
        public async Task<IActionResult> UpdateMessage(
            Guid messageId,
            [FromBody] UpdateMessageRequest request)
        {
            var message = await _messageRepo.GetMessageByIdAsync(messageId);

            if (message == null)
                return NotFound();

            var userId = User.GetCurrentUserId();

            // Chỉ người gửi mới sửa được
            if (message.UserId != userId)
                return Forbid();

            await _messageRepo.UpdateMessageAsync(messageId, request.Content);

            var updatedMessage = await _messageRepo.GetMessageByIdAsync(messageId);
            return Ok(new { success = true, data = updatedMessage });
        }

        // DELETE: api/messages/{messageId}
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            var message = await _messageRepo.GetMessageByIdAsync(messageId);

            if (message == null)
                return NotFound();

            var userId = User.GetCurrentUserId();
            var member = await _joinGroupRepo.GetMemberByUserAndGroupAsync(userId, message.GroupId);

            // Người gửi hoặc admin/owner mới xóa được
            if (message.UserId != userId && member?.Role != "owner" && member?.Role != "admin")
                return Forbid();

            await _messageRepo.DeleteMessageAsync(messageId);
            return Ok(new { success = true, message = "Message deleted" });
        }

        // GET: api/messages/groups/{groupId}/search
        [HttpGet("groups/{groupId}/search")]
        public async Task<IActionResult> SearchMessages(
            Guid groupId,
            [FromQuery] string keyword)
        {
            var userId = User.GetCurrentUserId();
            var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);

            if (!isMember)
                return Forbid();

            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(new { success = false, message = "Keyword is required" });

            var messages = await _messageRepo.SearchMessagesAsync(groupId, keyword);
            return Ok(new { success = true, data = messages });
        }

        // GET: api/messages/groups/{groupId}/type/{typeMessage}
        [HttpGet("groups/{groupId}/type/{typeMessage}")]
        public async Task<IActionResult> GetMessagesByType(Guid groupId, string typeMessage)
        {
            var userId = User.GetCurrentUserId();
            var isMember = await _joinGroupRepo.IsUserInGroupAsync(userId, groupId);

            if (!isMember)
                return Forbid();

            var messages = await _messageRepo.GetMessagesByTypeAsync(groupId, typeMessage);
            return Ok(new { success = true, data = messages });
        }

        // GET: api/messages/my-messages
        [HttpGet("my-messages")]
        public async Task<IActionResult> GetMyMessages()
        {
            var userId = User.GetCurrentUserId();
            var messages = await _messageRepo.GetMessagesByUserIdAsync(userId);
            return Ok(new { success = true, data = messages });
        }

    }
}
