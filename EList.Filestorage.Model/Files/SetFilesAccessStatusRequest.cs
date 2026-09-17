namespace EList.Filestorage.Model.Files
{
    public class SetFilesAccessStatusRequest
    {
        public List<Guid> FileIds { get; set; }

        public FileAccessStatus AccessStatus { get; set; }
    }
}
