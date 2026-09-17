using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Filestorage.Api.Filters;
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
    /// Internal API for elist.api (service-token only). Not for browser/UI clients.
    /// </summary>
    [ApiController]
    [Authorize]
    [ServiceTokenRequired]
    [Route("api/internal")]
    public class InternalFileStorageController : ControllerBase
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Controllers.InternalFileStorageController.";
        #endregion

        private readonly IFileStorageService _fileStorageService;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        public InternalFileStorageController(
            ICorrelationIdProvider correlationIdProvider,
            IFileStorageService fileStorageService)
        {
            _correlationIdProvider = correlationIdProvider;
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Batch Public/Private visibility sync (event/album privacy).
        /// </summary>
        [HttpPost("setVisibility")]
        public async Task<CommandResult> SetFilesVisibilityAsync([FromBody] SetFilesVisibilityRequest request)
        {
            return await ExecuteAsync(nameof(SetFilesVisibilityAsync),
                () => _fileStorageService.SetFilesVisibilityAsync(request));
        }

        /// <summary>
        /// Batch Active/Blocked accessStatus (moderation). Blob is kept on Block.
        /// </summary>
        [HttpPost("setAccessStatus")]
        public async Task<CommandResult> SetFilesAccessStatusAsync([FromBody] SetFilesAccessStatusRequest request)
        {
            return await ExecuteAsync(nameof(SetFilesAccessStatusAsync),
                () => _fileStorageService.SetFilesAccessStatusAsync(request));
        }

        private async Task<CommandResult> ExecuteAsync(string shortName, Func<Task<CommandResult>> action)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{shortName}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var result = await action();
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult.Fail(1, ex.Message);
            }
        }
    }
}
