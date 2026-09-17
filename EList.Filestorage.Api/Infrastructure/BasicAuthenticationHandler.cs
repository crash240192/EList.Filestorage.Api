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
                // Service-token (elist.api): tokenRegistration + internal delete/info
                var serviceAuth = TryAuthenticateServiceToken();
                if (serviceAuth != null)
                    return serviceAuth;

                if (IsTokenRegistrationPath())
                    return AuthenticateResult.Fail("Missing or invalid Authorization Header");

                return await CheckUserAuthenticationDataAsync();
            }
            catch (Exception exception)
            {
                #region logger
                logger.Error(correlationId, null, METHOD_NAME, "Invalid Authorization Header", null, exception, null);
                #endregion
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }
        }

        private bool IsTokenRegistrationPath()
        {
            return (Request.Path == "/api/tokenRegistration/register"
                    || Request.Path == "/api/tokenRegistration/disable")
                   && Request.Method == "POST";
        }

        /// <summary>
        /// Accept bare GUID or "Bearer {guid}" as the shared service token.
        /// Returns null when the request is not a service-token call.
        /// </summary>
        private AuthenticateResult? TryAuthenticateServiceToken()
        {
            var correlationId = _correlationIdProvider.Get();
            var METHOD_NAME = LOGGER_NAME + nameof(TryAuthenticateServiceToken);

            // No service token configured: keep legacy open access for tokenRegistration only
            if (token == null)
            {
                if (IsTokenRegistrationPath())
                {
                    _authorizationDataStorage.SetServiceRequest();
                    var claims = Array.Empty<Claim>();
                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
                }
                return null;
            }

            if (!Request.Headers.ContainsKey("Authorization"))
                return null;

            if (!TryParseAuthorizationGuid(Request.Headers["Authorization"], out var headerToken))
                return null;

            if (headerToken != token.Value)
                return null;

            _authorizationDataStorage.SetServiceRequest();

            var serviceClaims = new[] { new Claim(ClaimTypes.PrimarySid, headerToken.ToString()) };
            var serviceIdentity = new ClaimsIdentity(serviceClaims, Scheme.Name);
            var servicePrincipal = new ClaimsPrincipal(serviceIdentity);

            logger.Debug(correlationId, null, METHOD_NAME, "Service token authenticated", null, null);
            return AuthenticateResult.Success(new AuthenticationTicket(servicePrincipal, Scheme.Name));
        }

        private static bool TryParseAuthorizationGuid(StringValues header, out Guid guid)
        {
            guid = Guid.Empty;
            var raw = header.ToString()?.Trim();
            if (string.IsNullOrEmpty(raw))
                return false;

            if (Guid.TryParse(raw, out guid))
                return true;

            // "Bearer {guid}" / "Basic {guid}"
            var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && Guid.TryParse(parts[1], out guid))
                return true;

            return false;
        }

        private async Task<AuthenticateResult> CheckUserAuthenticationDataAsync()
        {
            var tokenHeader = Request.Headers.ContainsKey("Authorization") ? Request.Headers["Authorization"] : StringValues.Empty;
            var jwtHeader = Request.Headers.ContainsKey("Authorization-jwt") ? Request.Headers["Authorization-jwt"] : StringValues.Empty;

            if (jwtHeader == StringValues.Empty)
                return AuthenticateResult.Fail("Invalid Authorization-jwt Header");

            // Must match elist.api: hash(hash(jwt)|platform|appVersion)
            var clientHash = GetClientHash(jwtHeader.ToString());

            var claims = new List<Claim> { new Claim(ClaimTypes.Hash, clientHash) };

            if (tokenHeader != StringValues.Empty)
                claims.Add(new Claim(ClaimTypes.PrimarySid, tokenHeader));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            if (!TryParseAuthorizationGuid(tokenHeader, out var tokenValue))
                return AuthenticateResult.Fail("Authorization header must be Guid");

            var authorizationItem = await _authorizationService.GetAsync(new Model.Authorization.AuthorizationDataRequest
            {
                Token = tokenValue,
                JwtHash = clientHash,
            });

            if (!authorizationItem.Success || authorizationItem == null)
                return AuthenticateResult.Fail("Invalid Authorization Header");

            if (!authorizationItem.Result.Active)
                return AuthenticateResult.Fail("Token inactive");

            await _authorizationDataStorage.SetAuthorizationData(tokenValue, clientHash);
            return AuthenticateResult.Success(ticket);
        }

        /// <summary>
        /// Same algorithm as elist.api AuthenticationHandler.GetClientHash.
        /// </summary>
        private string GetClientHash(string jwtHeader)
        {
            var jwtHash = _encryptionTool.CalculateStringHash(jwtHeader);
            var platform = Request.Headers["X-Client-Platform"].FirstOrDefault() ?? "unknown";
            var appVersion = Request.Headers["X-App-Version"].FirstOrDefault() ?? "unknown";

            return _encryptionTool.CalculateStringHash($"{jwtHash}|{platform}|{appVersion}");
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
