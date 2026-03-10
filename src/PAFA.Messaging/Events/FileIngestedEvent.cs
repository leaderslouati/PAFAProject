namespace PAFA.Messaging.Events ; 
    public class FileIngestedEvent
    {
        public Guid FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; 
        public DateTime IngestedAt { get; set; }
        public string StoragePath { get; set; } = string.Empty; 
    }
