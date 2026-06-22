using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Filestorage.Core;
using EList.Filestorage.Model.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System.Diagnostics;
using ILogger = NLog.ILogger;

namespace EList.Filestorage.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    //[Authorize]
    [Route("api")]
    public class FileStorageController : ControllerBase
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Controllers.FileStorageController.";
        #endregion

        private readonly IFileStorageService _fileStorageService;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        /// <summary>
        /// Контроллер доступа к файлам
        /// </summary>
        /// <param name="correlationIdProvider"></param>
        /// <param name="fileStorageService"></param>
        public FileStorageController(ICorrelationIdProvider correlationIdProvider,
            IFileStorageService fileStorageService)
        {
            _correlationIdProvider = correlationIdProvider;
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Отправить файл в хранилище
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost("upload")]
        [RequestSizeLimit(42949672960)]
        public async Task<CommandResult<UploadFileResult>> UploadFileAsync(IFormFile formFile)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(UploadFileAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await _fileStorageService.SaveFileAsync(formFile);
                logger.Debug(correlationId, null, methodName,"Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult<UploadFileResult>.Fail(1, ex.Message);
            }
        }

        /// <summary>
        /// Отправить файл в хранилище
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpPost("upload/{fileName}")]
        [RequestSizeLimit(42949672960)]
        public async Task<CommandResult<UploadFileResult>> UploadFileAsync(string fileName)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(UploadFileAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var contentLength = Request.ContentLength;
                var result = await _fileStorageService.SaveFileAsync(fileName, HttpContext.Request.Body, contentLength);
                logger.Debug(correlationId, null, methodName,"Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult<UploadFileResult>.Fail(1, ex.Message);
            }
        }

        /// <summary>
        /// Отправить файл в хранилище (с доп. информацией)
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="fileContext"></param>
        /// <returns></returns>
        [HttpPost("attachContext")]
        public async Task<CommandResult> AttachFileContextAsync([FromQuery] Guid fileId, FileContext fileContext)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(AttachFileContextAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await _fileStorageService.AttachFileContextAsync(fileId, fileContext);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult.Fail(1, ex.Message);
            }
        }

        /// <summary>
        /// Скачать файл из хранилища
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="fullSize"></param>
        /// <returns></returns>
        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> DownloadFileAsync(Guid fileId, [FromHeader(Name = "FullSize")] bool? fullSize)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(DownloadFileAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await _fileStorageService.GetFileAsync(fileId, fullSize);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return File(result.Stream, result.ContentType, result.FileName);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Получить метаданные файла
        /// </summary>
        [HttpGet("info/{id}")]
        public async Task<CommandResult<Model.Files.FileInfo>> GetFileInfoAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetFileInfoAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await _fileStorageService.GetFileInfoAsync(id);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Удаление файла из хранилища
        /// </summary>
        [HttpDelete("delete/{id}")]
        public async Task<CommandResult> DeleteFileInfoAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetFileInfoAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await _fileStorageService.DeleteFileAsync(id);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }
    }
}
