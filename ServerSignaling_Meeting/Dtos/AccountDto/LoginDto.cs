using System.ComponentModel.DataAnnotations;

namespace ServerSignaling_Meeting.Dtos.Account
{
    public class LoginDto
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }
}
