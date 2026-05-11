namespace EList.Filestorage.Core
{
    public interface IFileRepository
    {
        Task<string> SaveAsync(Guid id, Stream stream);
        Task<Stream?> LoadAsync(Guid id);
        Task DeleteAsync(Guid id);
        Task<bool> CheckFileExistsAsync(Guid id);
    }
}
