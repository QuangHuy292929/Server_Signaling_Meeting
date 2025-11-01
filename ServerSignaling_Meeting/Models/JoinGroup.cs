using System.ComponentModel.DataAnnotations.Schema;

namespace ServerSignaling_Meeting.Models
{
    [Table("JoinGroups")]
    public class JoinGroup
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser User { get; set; }
        public Guid GroupId { get; set; }
        public GroupChat GroupChat { get; set; }
        public string Role { get; set; } // owner, member
        public string Status { get; set; } // JOIN, LEAVE, BLOCK
    }
}
