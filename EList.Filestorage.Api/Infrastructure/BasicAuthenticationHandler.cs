using EList.Common.Constants;
using EList.Common.CorrelationId;
using EList.Common.Encryption;
using EList.Common.Logger;
using EList.Filestorage.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NLog;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ConfigurationManager = EList.Common.Configuration.ConfigurationManager;
using ILogger = NLog.ILogger;

namespace EList.Filestorage.Api.Infrastructure
{
    /// <summary>
    /// Хендлер выполнения авторизации на API контроллере
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        #region NLog
        private static ILogger log = LogManager.GetCurrentClassLogger();
        private static ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Api.Infrastructure.BasicAuthenticationHandler";
        #endregion

        private const string TOKEN_PATH = "authorization:token";

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IAuthorizationService _authorizationService;
        private readonly IEncryptionTool _encryptionTool;
        private readonly IAuthorizationDataStorage _authorizationDataStorage;
        private Guid? token;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            ICorrelationIdProvider correlationIdProvider,
            IEncryptionTool encryptionTool,
            IAuthorizationService authorizationService,
            IAuthorizationDataStorage authorizationDataStorage) : base(options, logger, encoder, clock)
        {
            _correlationIdProvider = correlationIdProvider;
            _authorizationService = authorizationService;
            _encryptionTool = encryptionTool;
            _authorizationDataStorage = authorizationDataStorage;

            if (ConfigurationManager.AppSettings.Contains(TOKEN_PATH))
                token = Guid.Parse(ConfigurationManager.AppSettings[TOKEN_PATH]);
        }

        /// <summary>
        /// Процесс проверки авторизационных данных
        /// </summary>
        /// <returns>Результат проверки</returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            CheckCorrelationIdHeader();

            #region logger
            var correlationId = _correlationIdProvider.Get();
            var METHOD_NAME = LOGGER_NAME + nameof(HandleAuthenticateAsync);

            logger.Debug(correlationId, null, METHOD_NAME, $"{nameof(HandleAuthenticateAsync)} method has started");
            #endregion

            try
            {
                if ((Request.Path == "/api/tokenRegistration/register" || Request.Path == "/api/tokenRegistration/disable") && Request.Method == "POST")
                {
                    return CheckEListMainHader();
                }
                else
                {
                    return await CheckUserAuthenticationDataAsync();
                }
            }
            catch (Exception exception)
            {
                #region logger
                logger.Error(correlationId, null, METHOD_NAME, "Invalid Authorization Header", null, exception, null);
                #endregion
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }
        }

        private async Task<AuthenticateResult> CheckUserAuthenticationDataAsync()
        {
            //logger.Debug("Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Get Authorization header");
            var tokenHeader = Request.Headers.ContainsKey("Authorization") ? Request.Headers["Authorization"] : StringValues.Empty;
            var jwtHeader = Request.Headers.ContainsKey("Authorization-jwt") ? Request.Headers["Authorization-jwt"] : StringValues.Empty;

            if (jwtHeader == StringValues.Empty)
                return AuthenticateResult.Fail("Invalid Authorization-jwt Header");

            var jwtHash = _encryptionTool.CalculateStringHash(jwtHeader);

            var claims = new List<Claim> { new Claim(ClaimTypes.Hash, jwtHash) };

            if (tokenHeader != StringValues.Empty)
                claims.Add(new Claim(ClaimTypes.PrimarySid, tokenHeader));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            if (!Request.Headers.ContainsKey("Authorization"))
            {
                //logger.Error("Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Missing Authorization Header");
                return AuthenticateResult.Fail("Missing Authorization Header");
            }

            var tokenIsGuid = Guid.TryParse(tokenHeader, out var tokenValue);
            if (!tokenIsGuid)
                return AuthenticateResult.Fail("Authorization header must be Guid");

            //logger.Debug($"Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Get Token by id: {tokenValue}");

            var authorizationItem = await _authorizationService.GetAsync(new Model.Authorization.AuthorizationDataRequest
            {
                Token = tokenValue,
                JwtHash = jwtHash,
            });

            if (!authorizationItem.Success || authorizationItem == null)
            {
                //logger.Error("Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Invalid Authorization Header");
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            if (!authorizationItem.Result.Active)
            {
                //logger.Error("Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Token inactive");
                return AuthenticateResult.Fail("Token inactive");
            }
            await _authorizationDataStorage.SetAuthorizationData(tokenValue, jwtHash);
            //logger.Debug("Start BasicAuthenticationHandler 'HandleAuthenticateAsync' method - Success");
            return AuthenticateResult.Success(ticket);
        }

        private AuthenticateResult CheckEListMainHader()
        {
            var correlationId = _correlationIdProvider.Get();
            var METHOD_NAME = LOGGER_NAME + nameof(CheckEListMainHader);

            if (token == null)
            {
                var claims = new Claim[0];
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            else
            {
                if (!Request.Headers.ContainsKey("Authorization"))
                {
                    #region logger
                    logger.Error(correlationId, null, METHOD_NAME, $"{nameof(HandleAuthenticateAsync)} method missing Authorization Header", null, null);
                    #endregion
                    return AuthenticateResult.Fail("Missing Authorization Header");
                }

                var header = Request.Headers["Authorization"];
                var authHeader = AuthenticationHeaderValue.Parse(header);
                var headerToken = Guid.Parse(authHeader.Parameter);
                #region logger
                logger.Debug(correlationId, null, METHOD_NAME, $"{nameof(HandleAuthenticateAsync)} method get token from header", null, null, new Dictionary<string, object> { { "id", headerToken } });
                #endregion

                if (headerToken != token)
                {
                    #region logger
                    logger.Error(correlationId, null, METHOD_NAME, "Invalid Authorization Header", null, null, null, new Dictionary<string, object> { { "token", token } });
                    #endregion
                    return AuthenticateResult.Fail("Invalid Authorization Header");
                }

                var claims = new[] { new Claim(ClaimTypes.PrimarySid, authHeader.Parameter) };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                #region logger
                logger.Debug(correlationId, null, METHOD_NAME, $"{nameof(HandleAuthenticateAsync)} method has finished", null, null);
                #endregion
                return AuthenticateResult.Success(ticket);
            }
        }

        /// <summary>
        /// Проверка наличия переданного CorrelationId идентификатора
        /// </summary>
        private void CheckCorrelationIdHeader()
        {
            var correlationIdValue = Request.Headers.ContainsKey(Constants.HttpHeaderKeys.CORRELATION_ID) ? Request.Headers[Constants.HttpHeaderKeys.CORRELATION_ID].ToString() : null;
            if (string.IsNullOrWhiteSpace(correlationIdValue))
                correlationIdValue = Request.Headers.ContainsKey("CorrelationId") ? Request.Headers["CorrelationId"].ToString() : null;

            if (string.IsNullOrWhiteSpace(correlationIdValue))
                correlationIdValue = Guid.NewGuid().ToString();

            if (Request.HttpContext.Items.ContainsKey(Constants.HttpHeaderKeys.CORRELATION_ID))
                Request.HttpContext.Items[Constants.HttpHeaderKeys.CORRELATION_ID] = correlationIdValue;
            else
                Request.HttpContext.Items.Add(new KeyValuePair<object, object>(Constants.HttpHeaderKeys.CORRELATION_ID, correlationIdValue));
        }
    }
}
