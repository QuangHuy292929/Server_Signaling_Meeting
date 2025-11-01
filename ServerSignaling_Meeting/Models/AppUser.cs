using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Models
{
    public class AppUser : IdentityUser<Guid>
    {
        public override string Email { get; set; }
        public List<JoinGroup> JoinGroups { get; set; } = new();
        public List<JoinMeeting> JoinMeetings { get; set; } = new();
        public List<ChatMessage> ChatMessages { get; set; } = new();
    }
}
