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
        Task DeleteAsync(Guid id);
        Task<List<FileInfoDto>> GetOldestAvailableLocalFileInfosAsync(int? take = 0);
    }
}
