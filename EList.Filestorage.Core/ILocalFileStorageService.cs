namespace EList.Filestorage.Core
{
    public interface ILocalFileStorage
    {
        Task<string> SaveAsync(Guid id, Stream readStream);
        Stream Load(Guid id);
        void Delete(Guid id);
        bool CheckFileExists(Guid id);
        /// <summary>Enumerate GUID-named files in the storage directory (capped).</summary>
        IReadOnlyList<Guid> EnumerateStoredIds(int maxCount);
    }
}
