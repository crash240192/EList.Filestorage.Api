namespace EList.Filestorage.Model.Files
{
    /// <summary>
    /// Batch-обновление visibility связанных файлов (internal / service-token).
    /// </summary>
    public class SetFilesVisibilityRequest
    {
        public List<Guid> FileIds { get; set; }

        public FileVisibility Visibility { get; set; }
    }
}
