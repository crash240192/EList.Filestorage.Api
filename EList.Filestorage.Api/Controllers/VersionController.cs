

//VersionController


/*using EList.Common.CorrelationId;
using EList.Common.Logger;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System.Diagnostics;
using System.Reflection;

namespace TM.Filestorage.Api.Controllers
{
    /// <summary>
    /// Контроллер версий
    /// </summary>
    [ApiController]
    public class VersionController : ControllerBase
    {

        #region logger
        private static readonly NLog.ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Controllers.VersionController.";
        #endregion

        private readonly ICorrelationIdProvider _correlationIdProvider;
        
        /// <summary>
        /// Контроллер версий
        /// </summary>
        public VersionController(ICorrelationIdProvider correlationIdProvider)
        {
            _correlationIdProvider = correlationIdProvider;
        }

        /// <summary>
        /// Получение текущей версии сервиса
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("/api/_version")]
        public async Task<GetVersionResponse> GetVersionAsync()
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetVersionAsync)}";

            try
            {
                logger.Debug(correlationId, null, methodName, null, $"Method started", null);

                var result = await VersionService.GetVersionAsync(Assembly.GetExecutingAssembly(), true);

                logger.Debug(correlationId, null, methodName, $"Method finished", null, execTime.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, ex.Message, execTime.Elapsed, ex);
                throw;
            }
        }
    }
}
*/
