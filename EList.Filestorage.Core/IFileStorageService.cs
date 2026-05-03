using EList.Common.Models;
using EList.Filestorage.Model.Files;
using Microsoft.AspNetCore.Http;

namespace EList.Filestorage.Core
{
    public interface IFileStorageService
    {
        Task<CommandResult<UploadFileResult>> SaveFileAsync(IFormFile file);
        Task<CommandResult<UploadFileResult>> SaveFileAsync(string fileName, Stream file,long? contentLength = null);
        Task<CommandResult> AttachFileContextAsync(Guid fileId, FileContext fileContext);
        Task<FileStreamContainer> GetFileAsync(Guid id, bool? fullSize = false);
        Task<CommandResult<Model.Files.FileInfo>> GetFileInfoAsync(Guid id);
    }
}