namespace ServerSignaling_Meeting.Dtos.RoomDto
{
    public class UpdateRoomRequest
    {
        public string? RoomName { get; set; }
        public int? MaxParticipants { get; set; }
        public bool? IsActive { get; set; }
    }
}
