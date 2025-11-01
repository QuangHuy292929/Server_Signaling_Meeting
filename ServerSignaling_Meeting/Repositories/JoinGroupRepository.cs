using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Data;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;

namespace ServerSignaling_Meeting.Repositories
{
    public class JoinGroupRepository : IJoinGroupRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JoinGroupRepository> _logger;
        public JoinGroupRepository(ApplicationDbContext context, ILogger<JoinGroupRepository> logger)
        {
            _context = context;
            _logger = logger;
        }


        //GET JOIN GROUP--------------------------------
        public async Task<int> GetGroupMembersCountAsync(Guid groupId)
        {
            return await _context.JoinGroups
                .Where(j => j.GroupId == groupId && j.Status == "JOIN")
                .CountAsync();
        }

        public async Task<IEnumerable<JoinGroup>> GetGroupsByUserIdAsync(Guid userId)
        {
            return await _context.JoinGroups
                .Include(j => j.GroupChat)
                .Where(j => j.UserId == userId)
                .ToListAsync();
        }

        public async Task<JoinGroup?> GetJoinByIdAsync(Guid id)
        {
            return await _context.JoinGroups
                .Include(j => j.GroupChat)
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        public async Task<JoinGroup?> GetMemberByUserAndGroupAsync(Guid userId, Guid groupId)
        {
            return await _context.JoinGroups
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.UserId == userId && j.GroupId == groupId);
        }

        public async Task<IEnumerable<JoinGroup>> GetMembersByGroupIdAsync(Guid groupId)
        {
            return await _context.JoinGroups
                .Include(j => j.User)
                .Where(j => j.GroupId == groupId)
                .ToListAsync();
        }

        public async Task<IEnumerable<JoinGroup>> GetMembersByStatusAsync(Guid groupId, string status)
        {
            return await _context.JoinGroups
                .Include(j => j.User)
                .Where(j => j.GroupId == groupId && j.Status == status)
                .ToListAsync();
        }


        //CREATE JOIN GROUP--------------------------------
        public async Task<JoinGroup> AddMemberToGroupAsync(Guid groupId, Guid userId, string role = "member")
        {
            var joinGroup = new JoinGroup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = userId,
                Role = role,
                Status = "JOIN"
            };

            await _context.JoinGroups.AddAsync(joinGroup);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} added to group {groupId}");
            return joinGroup;
        }


        //ACTIVITIES WITH MEMBERS
        public async Task BlockMemberAsync(Guid groupId, Guid userId)
        {
            var member = await GetMemberByUserAndGroupAsync(userId, groupId);
            if (member != null)
            {
                member.Status = "BLOCK";
                _context.JoinGroups.Update(member);
                await _context.SaveChangesAsync();
            }
        }
        public async Task RemoveMemberFromGroupAsync(Guid groupId, Guid userId)
        {
            var member = await GetMemberByUserAndGroupAsync(userId, groupId);
            if (member != null)
            {
                member.Status = "LEAVE";
                _context.JoinGroups.Update(member);
                await _context.SaveChangesAsync();
            }
        }
        public async Task UnblockMemberAsync(Guid groupId, Guid userId)
        {
            var member = await GetMemberByUserAndGroupAsync(userId, groupId);
            if (member != null)
            {
                member.Status = "BLOCK";
                _context.JoinGroups.Update(member);
                await _context.SaveChangesAsync();
            }
        }
        public async Task UpdateMemberRoleAsync(Guid groupId, Guid userId, string role)
        {
            var member = await GetMemberByUserAndGroupAsync(userId, groupId);
            if (member != null)
            {
                member.Role = role;
                _context.JoinGroups.Update(member);
                await _context.SaveChangesAsync();
            }
        }
        public async Task<bool> IsUserInGroupAsync(Guid userId, Guid groupId)
        {
            return await _context.JoinGroups
            .AnyAsync(j => j.UserId == userId && j.GroupId == groupId && j.Status == "JOIN");
        }
    }
}
