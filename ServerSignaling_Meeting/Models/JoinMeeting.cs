using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Models
{
    [Table("JoinMeetings")]
    public class JoinMeeting
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public Guid RoomId { get; set; }
        public RoomMeeting RoomMeeting { get; set; }

        public string Role { get; set; } // host, participant
        public string status { get; set; }
        public DateTime? JoinAt { get; set; }
        public DateTime? LeaveAt { get; set; }
    }
}
