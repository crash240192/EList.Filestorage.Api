using EList.Filestorage.Data.Linq2db.Dto;

namespace EList.Filestorage.Data.Linq2db.Interfaces
{
    public interface IDBStorageDataProvider
    {
        Task SaveAsync(Guid id, Stream stream);
        Task DeleteAsync(Guid id);
        Task<Stream> LoadAsync(Guid id);
        Task<bool> CheckFileExistAsync(Guid id);
    }
}
