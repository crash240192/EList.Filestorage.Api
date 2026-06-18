using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Filestorage.BackgroundWorker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System.Diagnostics;

namespace EList.Filestorage.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UploaderController : ControllerBase
    {
        #region logger
        private static readonly NLog.ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Controllers.UploaderController.";
        #endregion

        private readonly IBackgroundWorkerService _backgroundUploaderService;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        public UploaderController(ICorrelationIdProvider  correlationIdProvider,
            IBackgroundWorkerService backgroundUploaderService)
        {
            _backgroundUploaderService = backgroundUploaderService;
            _correlationIdProvider = correlationIdProvider;
        }


        /// <summary>
        /// Запускает выгрузчик
        /// </summary>
        /// <returns></returns>
        [HttpGet("start")]
        public CommandResult StartUploader()
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(StartUploader)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                _backgroundUploaderService.ManualStart();
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return CommandResult.OK;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Останавливает выгрузчик
        /// </summary>
        /// <returns></returns>
        [HttpGet("stop")]
        public CommandResult StopUploader()
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(StopUploader)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                _backgroundUploaderService.ManualStop();
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return CommandResult.OK;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Статус активности выгрузчика
        /// </summary>
        /// <returns></returns>
        [HttpGet("status")]
        public CommandResult<bool> GetStatus()
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetStatus)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                var status = _backgroundUploaderService.Active;

                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return new CommandResult<bool>(status);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Запуск одной итерации выгрузки в указанное количество потоков
        /// </summary>
        /// <returns></returns>
        [HttpGet("sendOneIteration")]
        public CommandResult SendOneIteration()
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(SendOneIteration)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                if (_backgroundUploaderService.Active)
                    return CommandResult.Fail(1, "Запуск единичной итерации выгрузки возможна только при остановленном выгрузчике");

                _backgroundUploaderService.Process();
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return CommandResult.OK;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                throw;
            }
        }
    }
}
