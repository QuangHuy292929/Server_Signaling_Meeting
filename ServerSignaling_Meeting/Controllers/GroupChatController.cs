using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerSignaling_Meeting.Dtos.Group;
using ServerSignaling_Meeting.Extensions;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SignalingServer.Controllers
{
    [Route("api/groupchat")]
    [ApiController]
    [Authorize]
    public class GroupChatController : ControllerBase
    {
        private readonly ILogger<GroupChatController> _logger;
        private readonly IGroupChatRepository _groupRepo;
        private readonly IJoinGroupRepository _jgrepo;

        public GroupChatController( 
            ILogger<GroupChatController> logger, 
            IGroupChatRepository groupChatRepository, 
            IJoinGroupRepository joinGroupRepository
        )
        {
            _logger = logger;
            _groupRepo = groupChatRepository;
            _jgrepo = joinGroupRepository;
        }

        //================================ GROUP CHAT ===============================//

        // GET: api/groups/my-groups
        [HttpGet("my-groups")]
        public async Task<IActionResult> GetMyGroups()
        {
            var userId = User.GetCurrentUserId();
            var groups = await _groupRepo.GetGroupsByUserIdAsync(userId);
            return Ok(new { success = true, data = groups });
        }

        // GET: api/groups/{groupId}
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupById(Guid groupId)
        {
            var userId = User.GetCurrentUserId();
            var isMember = await _jgrepo.IsUserInGroupAsync(userId, groupId);

            if (!isMember)
                return Forbid();

            var group = await _groupRepo.GetGroupByIdAsync(groupId);
            return Ok(new { success = true, data = group });
        }

        // POST: api/groups
        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            var userId = User.GetCurrentUserId();
            var group = await _groupRepo.CreateGroupAsync(request.GroupName, userId);

            return Ok(new { success = true, data = group });
        }

        // PUT: api/groups/{groupId}
        [HttpPut("{groupId}")]
        public async Task<IActionResult> UpdateGroup(Guid groupId, [FromBody] UpdateGroupRequest request)
        {
            var userId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(userId, groupId);

            if (member?.Role != "owner")
                return Forbid();

            var group = await _groupRepo.GetGroupByIdAsync(groupId);
            if (group == null)
                return NotFound();

            group.GroupName = request.GroupName;
            await _groupRepo.UpdateGroupAsync(group);

            return Ok(new { success = true, data = group });
        }

        // DELETE: api/groups/{groupId}
        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup(Guid groupId)
        {
            var userId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(userId, groupId);

            if (member?.Role != "owner")
                return Forbid();

            await _groupRepo.DeleteGroupAsync(groupId);
            return Ok(new { success = true, message = "Group deleted" });
        }

        // POST: api/groups/{groupId}/block
        [HttpPost("{groupId}/block")]
        public async Task<IActionResult> BlockGroup(Guid groupId)
        {
            var userId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(userId, groupId);

            if (member?.Role != "owner")
                return Forbid();

            await _groupRepo.BlockGroupAsync(groupId);
            return Ok(new { success = true, message = "Group blocked" });
        }

        //================================ MEMBER ===============================//

        // GET: api/groups/{groupId}/members
        [HttpGet("{groupId}/members")]
        public async Task<IActionResult> GetGroupMembers(Guid groupId)
        {
            var userId = User.GetCurrentUserId();
            var isMember = await _jgrepo.IsUserInGroupAsync(userId, groupId);

            if (!isMember)
                return Forbid();

            var members = await _jgrepo.GetMembersByGroupIdAsync(groupId);
            var memberCount = await _jgrepo.GetGroupMembersCountAsync(groupId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    members = members,
                    totalCount = memberCount
                }
            });
        }

        // POST: api/groups/{groupId}/members
        [HttpPost("{groupId}/members")]
        public async Task<IActionResult> AddMembers(
            Guid groupId,
            [FromBody] AddMembersRequest request)
        {
            var userId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(userId, groupId);

            // Chỉ owner và admin mới add được
            if (member?.Role != "owner" && member?.Role != "admin")
                return Forbid();

            var addedMembers = new List<JoinGroup>();
            foreach (var memberId in request.UserIds)
            {
                var newMember = await _jgrepo.AddMemberToGroupAsync(
                    groupId, memberId, "member");
                addedMembers.Add(newMember);
            }

            return Ok(new { success = true, data = addedMembers });
        }

        // DELETE: api/groups/{groupId}/members/{userId}
        [HttpDelete("{groupId}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId)
        {
            var currentUserId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(currentUserId, groupId);

            // Tự rời hoặc owner/admin kick
            if (currentUserId != userId && member?.Role != "owner" && member?.Role != "admin")
                return Forbid();

            await _jgrepo.RemoveMemberFromGroupAsync(groupId, userId);
            return Ok(new { success = true, message = "Member removed" });
        }

        // PUT: api/groups/{groupId}/members/{userId}/role
        [HttpPut("{groupId}/members/{userId}/role")]
        public async Task<IActionResult> UpdateMemberRole(
            Guid groupId,
            Guid userId,
            [FromBody] UpdateRoleRequest request)
        {
            var currentUserId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(currentUserId, groupId);

            if (member?.Role != "owner")
                return Forbid();

            await _jgrepo.UpdateMemberRoleAsync(groupId, userId, request.Role);
            return Ok(new { success = true, message = "Role updated" });
        }

        // POST: api/groups/{groupId}/members/{userId}/block
        [HttpPost("{groupId}/members/{userId}/block")]
        public async Task<IActionResult> BlockMember(Guid groupId, Guid userId)
        {
            var currentUserId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(currentUserId, groupId);

            if (member?.Role != "owner" && member?.Role != "admin")
                return Forbid();

            await _jgrepo.BlockMemberAsync(groupId, userId);
            return Ok(new { success = true, message = "Member blocked" });
        }

        // POST: api/groups/{groupId}/members/{userId}/unblock
        [HttpPost("{groupId}/members/{userId}/unblock")]
        public async Task<IActionResult> UnblockMember(Guid groupId, Guid userId)
        {
            var currentUserId = User.GetCurrentUserId();
            var member = await _jgrepo.GetMemberByUserAndGroupAsync(currentUserId, groupId);

            if (member?.Role != "owner" && member?.Role != "admin")
                return Forbid();

            await _jgrepo.UnblockMemberAsync(groupId, userId);
            return Ok(new { success = true, message = "Member unblocked" });
        }

        // Join group chat
        // POST: api/groupchat/{groupId}/join
        [HttpPost("{groupId}/join")]
        public async Task<IActionResult> JoinGroup(Guid groupId)
        {
            var userId = User.GetCurrentUserId();

            // Nếu đã trong group rồi
            if (await _jgrepo.IsUserInGroupAsync(userId, groupId))
                return Ok(new { success = false, message = "Already joined" });

            var member = await _jgrepo.AddMemberToGroupAsync(groupId, userId, "member");
            return Ok(new { success = true, data = member });
        }

    }
}
