using EList.Filestorage.Data.Linq2db.Dto;

namespace EList.Filestorage.Data.Linq2db.Interfaces
{
    public interface IFileInfoDataProvider
    {
        void Configure(string connectionStringName);
        Task<FileInfoDto?> GetAsync(Guid id);
        Task<List<FileInfoDto>> GetListAsync(List<Guid> ids);
        Task<FileInfoDto> CreateAsync(FileInfoDto item);
        Task<FileInfoDto> UpdateAsync(FileInfoDto item);
        Task UpdateFilePreviewIdAsync(Guid fileId, Guid previewId);
        Task UpdateVisibilityAsync(IReadOnlyList<Guid> fileIds, short visibility);
        Task UpdateAccessStatusAsync(IReadOnlyList<Guid> fileIds, short accessStatus);
        Task DeleteAsync(Guid id);
        Task<List<FileInfoDto>> GetOldestAvailableLocalFileInfosAsync(int? take = 0);
        Task<List<FileInfoDto>> GetGcCandidatesAsync(DateTimeOffset olderThan, int take, Guid? afterId);
    }
}
