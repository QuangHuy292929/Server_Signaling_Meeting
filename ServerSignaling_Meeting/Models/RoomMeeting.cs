using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Models
{
    [Table("RoomMeetings")]
    public class RoomMeeting
    {
        public Guid Id { get; set; }
        public string RoomKey { get; set; }
        public string RoomName { get; set; }

        public int Max { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; }

        public List<JoinMeeting> JoinMeetings { get; set; } = new();
    }
}
