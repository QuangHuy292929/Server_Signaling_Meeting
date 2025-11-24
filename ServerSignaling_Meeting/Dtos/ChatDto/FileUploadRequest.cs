namespace ServerSignaling_Meeting.Dtos.ChatDto
{
   
        public class FileUploadRequest
        {
            public IFormFile File { get; set; } = null!;
            public string FileType { get; set; } = string.Empty;
        }
   
}
