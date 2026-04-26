using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Filestorage.Model.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System.Diagnostics;
using IAuthorizationService = EList.Filestorage.Core.IAuthorizationService;

namespace EList.Filestorage.Api.Controllers
{
    /// <summary>
    /// Регистрация авторизационных данных eList
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TokenRegistrationController : ControllerBase
    {
        #region logger
        private static readonly NLog.ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Controllers.TokenRegistrationController.";
        #endregion

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IAuthorizationService _authorizationService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="correlationIdProvider"></param>
        /// <param name="authorizationService"></param>
        public TokenRegistrationController(ICorrelationIdProvider correlationIdProvider,
            IAuthorizationService authorizationService)
        {
            _correlationIdProvider = correlationIdProvider;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Создание нового токена пользователя eList
        /// </summary>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<CommandResult> RegisterTokenAsync(AuthorizationDataRequest request)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(RegisterTokenAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                await _authorizationService.CreateOrActivateAsync(request);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return CommandResult.OK;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult.Fail(1, ex.Message);
            }
        }

        /// <summary>
        /// Отключение токена пользователя eList
        /// </summary>
        /// <returns></returns>
        [HttpPost("disable")]
        public async Task<CommandResult> DisableTokenAsync(AuthorizationDataRequest request)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(RegisterTokenAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");
            try
            {
                await _authorizationService.DisableAsync(request);
                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return CommandResult.OK;
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName, $"Method failed: {ex.Message}", execTime.Elapsed, ex);
                return CommandResult.Fail(1, ex.Message);
            }
        }
    }
}
