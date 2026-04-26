using EList.Common.CorrelationId;
using EList.Common.Encryption;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Model.Authorization;
using NLog;
using System.Diagnostics;

namespace EList.Filestorage.Core.Impl
{
    public class AuthorizationService : IAuthorizationService
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Core.Impl.AuthorizationService.";
        #endregion

        private readonly IAuthorizationDataProvider _authorizationDataProvider;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IEncryptionTool _encryptionTool;

        public AuthorizationService(ICorrelationIdProvider correlationIdProvider,
            IAuthorizationDataProvider authorizationDataProvider,
            IEncryptionTool encryptionTool) 
        {
            _authorizationDataProvider = authorizationDataProvider;
            _correlationIdProvider = correlationIdProvider;
            _encryptionTool = encryptionTool;
        }

        public async Task<CommandResult> CreateOrActivateAsync(AuthorizationDataRequest request)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(CreateOrActivateAsync)}";
            logger.Debug(correlationId, null, methodName, "Method started", null, execTime.Elapsed);

            if (request.AccountId == null)
                return CommandResult.Fail(1, "Не указан accountId");

            await _authorizationDataProvider.CreateOrActivateAsync(new Filestorage.Data.Linq2db.Dto.AuthorizationDataDto
            {
                AccountId = request.AccountId.Value,
                JwtHash = request.JwtHash,
                Token = request.Token
            });

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return CommandResult.OK;
        }

        public async Task<CommandResult> DisableAsync(AuthorizationDataRequest request)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(CreateOrActivateAsync)}";
            logger.Debug(correlationId, null, methodName, "Method started", null, execTime.Elapsed);

            await _authorizationDataProvider.DisableAsync(request.Token, request.JwtHash);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return CommandResult.OK;
        }

        public async Task<CommandResult<AuthorizationData>> GetAsync(AuthorizationDataRequest request)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetAsync)}";
            logger.Debug(correlationId, null, methodName, "Method started", null, execTime.Elapsed);

            var authData = await _authorizationDataProvider.GetAsync(request.Token, request.JwtHash);

            if (authData == null) 
                return CommandResult<AuthorizationData>.Fail(1, "Авторизационные данные не найдены");

            var result = new AuthorizationData
            {
                AccountId = authData.AccountId, 
                Token = authData.Token, 
                Active = authData.Active, 
                CreateDate = authData.CreateDate, 
                JwtHash = authData.JwtHash, 
                UpdateDate = authData.UpdateDate
            };

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return new CommandResult<AuthorizationData>(result);
        }
    }
}
