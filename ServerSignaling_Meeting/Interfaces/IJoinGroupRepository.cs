using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface IJoinGroupRepository
    {
        Task<JoinGroup?> GetJoinByIdAsync(Guid id);
        Task<IEnumerable<JoinGroup>> GetMembersByGroupIdAsync(Guid groupId);
        Task<IEnumerable<JoinGroup>> GetGroupsByUserIdAsync(Guid userId);
        Task<JoinGroup?> GetMemberByUserAndGroupAsync(Guid userId, Guid groupId);
        Task<IEnumerable<JoinGroup>> GetMembersByStatusAsync(Guid groupId, string status);
        Task<int> GetGroupMembersCountAsync(Guid groupId);
        Task<bool> IsUserInGroupAsync(Guid userId, Guid groupId);
        Task<JoinGroup> AddMemberToGroupAsync(Guid groupId, Guid userId, string role = "member");
        Task RemoveMemberFromGroupAsync(Guid groupId, Guid userId);
        Task UpdateMemberRoleAsync(Guid groupId, Guid userId, string role);
        Task BlockMemberAsync(Guid groupId, Guid userId);
        Task UnblockMemberAsync(Guid groupId, Guid userId);
    }
}
