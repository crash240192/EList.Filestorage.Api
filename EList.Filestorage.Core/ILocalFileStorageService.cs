namespace EList.Filestorage.Core
{
    public interface ILocalFileStorage
    {
        Task SaveAsync(Guid id, Stream readStream);
        Stream Load(Guid id);
        void Delete(Guid id);
        bool CheckFileExists(Guid id);
    }
}
