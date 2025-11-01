namespace ServerSignaling_Meeting.Dtos.RoomDto
{
    public class CreateRoomRequest
    {
        public string RoomName { get; set; } = string.Empty;
        public int MaxParticipants { get; set; }
    }
}
