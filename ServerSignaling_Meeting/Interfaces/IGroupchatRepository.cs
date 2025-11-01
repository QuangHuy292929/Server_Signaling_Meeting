using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface IGroupChatRepository 
    {
        Task<GroupChat?> GetGroupByIdAsync(Guid groupId);
        Task<GroupChat?> GetGroupByKeyAsync(string groupKey);
        Task<IEnumerable<GroupChat?>> GetGroupsByStatusAsync(string status);
        Task<IEnumerable<GroupChat?>> GetGroupsByUserIdAsync(Guid userId);
        Task<GroupChat?> CreateGroupAsync(string groupName, Guid creatorId);
        Task UpdateGroupAsync(GroupChat groupChat);
        Task DeleteGroupAsync(Guid groupId);
        Task BlockGroupAsync(Guid groupId);
    }
}
