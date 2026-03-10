namespace PAFA.Domain.Entities
{
    public class IngestedFile : BaseEntity
    {
        public string FileName { get; private set; }
        public string FileType { get; private set; }
        public string Status { get; private set; } 

        protected IngestedFile() { }

        // Factory Method pour garantir un état valide à la création
        public static IngestedFile Create(string fileName)
        {
            return new IngestedFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                FileType = fileName.EndsWith(".xml") ? "XML" : "Excel",
                Status = "Received",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void MarkAsProcessing()
        {
            Status = "Processing";
        }
    }
}