using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Models
{ 
    [Table("ChatMessages")]
    public class ChatMessage
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }
        public GroupChat GroupChat { get; set; }

        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public string TypeMessage { get; set; } // text, image, file...
        public string ContentMessage { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }

        public DateTime SendAt { get; set; }
    }
}
