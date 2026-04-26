namespace EList.Filestorage.Model.Files
{
    public class FileInfo
    {
        public Guid Id { get; set; }
        public string MimeType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<Metadata> Metadata { get; set; }
        public string Url { get; set; }
    }

    public class Metadata
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
