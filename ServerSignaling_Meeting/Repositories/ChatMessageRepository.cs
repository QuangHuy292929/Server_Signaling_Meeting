using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Data;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Repositories
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatMessageRepository> _logger;
        public ChatMessageRepository(ApplicationDbContext context, ILogger<ChatMessageRepository> logger)
        {
            _context = context;
            _logger = logger;
        }


        //GET MESSAGE--------------------------------
        public async Task<ChatMessage?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.ChatMessages
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesByGroupIdAsync(Guid groupId, int take = 50, int skip = 0)
        {
            return await _context.ChatMessages
                .Where(m => m.GroupId == groupId)
                .OrderByDescending(m => m.SendAt)
                .Skip(skip)
                .Take(take)
                .Include(m => m.User)
                .ToListAsync();
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(Guid userId)
        {
            return await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesByTypeAsync(Guid groupId, string typeMessage)
        {
            return await _context.ChatMessages
                .Where(m => m.GroupId == groupId && m.TypeMessage == typeMessage)
                .ToListAsync();
        }

        public async Task<int> GetGroupMessageCountAsync(Guid groupId)
        {
            return await _context.ChatMessages
                .Where(m => m.GroupId == groupId)
                .CountAsync();
        }

        //SAVE MESSAGE--------------------------------
        public async Task<ChatMessage> SaveMessageAsync(ChatMessage message)
        {
            // Kiểm tra null
            if (message == null)
            {
                _logger.LogWarning("Attempted to save null message");
                throw new ArgumentNullException(nameof(message));
            }

            // Tạo ID + Timestamp
            message.Id = Guid.NewGuid();
            message.SendAt = DateTime.UtcNow;

            // Add vào context
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            // Log
            _logger.LogInformation($"Message saved: {message.Id} to group {message.GroupId} by user {message.UserId}");

            // Return message
            return message;
        }


        //UPDATE & DELETE MESSAGE----------------------
        public async Task UpdateMessageAsync(Guid messageId, string content)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message != null)
            {
                message.ContentMessage = content;
                _context.ChatMessages.Update(message);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteMessageAsync(Guid messageId)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message != null)
            {
                _context.ChatMessages.Remove(message);
                await _context.SaveChangesAsync();
            }
        }


        //SEARCH MESSAGE--------------------------------
        public async Task<IEnumerable<ChatMessage>> SearchMessagesAsync(Guid groupId, string keyword)
        {
            return await _context.ChatMessages
                .Where(m => m.GroupId == groupId && m.ContentMessage.Contains(keyword))
                .ToListAsync();
        }
    }
}
