namespace ServerSignaling_Meeting.Dtos.Account
{
    public class NewAccountDto
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Token { get; set; }
    }
}
