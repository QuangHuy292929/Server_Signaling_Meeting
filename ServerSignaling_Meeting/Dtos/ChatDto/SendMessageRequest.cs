namespace ServerSignaling_Meeting.Dtos.ChatDto
{
    public class SendMessageRequest
    {
        public string TypeMessage { get; set; } // Loại tin nhắn: text, image, file
        public string Content { get; set; }     // Nội dung tin nhắn (text)
        public string? FileName { get; set; }    // Tên file nếu là image/file (tùy chọn)
        public string? FileUrl { get; set; }     // URL file nếu là image/file (tùy chọn)
    }
}
