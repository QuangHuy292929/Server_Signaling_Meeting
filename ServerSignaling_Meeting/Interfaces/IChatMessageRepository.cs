using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface IChatMessageRepository
    {
        // GET
        Task<ChatMessage?> GetMessageByIdAsync(Guid messageId);
        Task<IEnumerable<ChatMessage>> GetMessagesByGroupIdAsync(Guid groupId, int take = 50, int skip = 0);
        Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(Guid userId);
        Task<IEnumerable<ChatMessage>> GetMessagesByTypeAsync(Guid groupId, string typeMessage);
        Task<int> GetGroupMessageCountAsync(Guid groupId);
        Task<IEnumerable<ChatMessage>> SearchMessagesAsync(Guid groupId, string keyword);

        // SAVE (từ SignalR)
        Task<ChatMessage> SaveMessageAsync(ChatMessage message);

        // UPDATE & DELETE
        Task UpdateMessageAsync(Guid messageId, string content);
        Task DeleteMessageAsync(Guid messageId);
    }
}
