
using System.ComponentModel.DataAnnotations.Schema;


namespace ServerSignaling_Meeting.Models
{
    [Table("GroupChats")]
    public  class GroupChat
    {
        public Guid Id { get; set; }
        public string GroupKey { get; set; }
        public string GroupName { get; set; }
        public string Status { get; set; } // ACTIVE, BLOCK

        public List<JoinGroup> JoinGroups { get; set; } = new();
        public List<ChatMessage> ChatMessages { get; set; } = new();
    }
}
