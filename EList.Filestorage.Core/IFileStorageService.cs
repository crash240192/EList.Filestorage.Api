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
        Task<CommandResult> SetFilesVisibilityAsync(SetFilesVisibilityRequest request);
        Task<CommandResult> SetFilesAccessStatusAsync(SetFilesAccessStatusRequest request);
        Task<FileStreamContainer> GetFileAsync(Guid id, bool? fullSize = false);
        /// <summary>
        /// True if current request may download the file
        /// (not Blocked; Public, or Private with auth/service).
        /// </summary>
        Task<CommandResult> AssertCanDownloadAsync(Guid id);
        Task<CommandResult<Model.Files.FileInfo>> GetFileInfoAsync(Guid id);
        Task<CommandResult> DeleteFileAsync(Guid id);
    }
}
