using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Data;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;
using System.Security.Cryptography;

namespace ServerSignaling_Meeting.Repositories
{
    public class GroupChatRepository : IGroupChatRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupChatRepository> _logger;

        public GroupChatRepository(ApplicationDbContext context, ILogger<GroupChatRepository> logger)
        {
            _context = context;
            _logger = logger;
        }
        

        // GET GROUP--------------------------------
        public async Task<GroupChat?> GetGroupByIdAsync(Guid groupId)
        {
            return await _context.GroupChats
                .Include(g => g.JoinGroups)
                .Include(g => g.ChatMessages)
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }

        public async Task<GroupChat?> GetGroupByKeyAsync(string groupKey)
        {
            return await _context.GroupChats.FirstOrDefaultAsync(g => g.GroupKey == groupKey);
        }

        public async Task<IEnumerable<GroupChat?>> GetGroupsByStatusAsync(string status)
        {
            return await _context.GroupChats
                .Where(g => g.Status == status)
                .ToListAsync();
        }

        public async Task<IEnumerable<GroupChat?>> GetGroupsByUserIdAsync(Guid userId)
        {
            return await _context.GroupChats
                .Where(g => g.JoinGroups.Any(jg => jg.UserId == userId))
                .ToListAsync();
        }

        public static string GenerateGroupKey(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var bytes = new byte[length];
            var result = new char[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }

            return new string(result);
            }

        //CREATE GROUP---------------------------------
        public async Task<GroupChat?> CreateGroupAsync(string groupName, Guid creatorId)
        {
            var groupKey = GenerateGroupKey(10);
            var group = new GroupChat
            {
                Id = Guid.NewGuid(),
                GroupKey = groupKey,
                GroupName = groupName,
                Status = "ACTIVE"
            };

            await _context.GroupChats.AddAsync(group);
            await _context.SaveChangesAsync();

            //Add creator as owner
            var joinGroup = new JoinGroup
            {
                Id = Guid.NewGuid(),
                UserId = creatorId,
                GroupId = group.Id,
                Role = "owner",
                Status = "JOIN"
            };

            await _context.JoinGroups.AddAsync(joinGroup);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Group '{groupName}' created by user {creatorId}");

            return group;
        }


        //UPDATE GROUP-------------------------------------------
        public async Task UpdateGroupAsync(GroupChat groupChat)
        {
            _context.GroupChats.Update(groupChat);
            await _context.SaveChangesAsync();

        }


        //DELETE GROUP-------------------------------------------
        public async Task DeleteGroupAsync(Guid groupId)
        {
            var group = await GetGroupByIdAsync(groupId);
            if (group != null)
            {
                _context.GroupChats.Remove(group);
                await _context.SaveChangesAsync();
            }
        }


        //BLOCK GROUP-------------------------------------------
        public async Task BlockGroupAsync(Guid groupId)
        {
            var groupchat = await GetGroupByIdAsync(groupId);
            if (groupchat != null)
            {
                groupchat.Status = "BLOCK";
                await UpdateGroupAsync(groupchat);
            }
        }
    }
}
