using EList.Common.CorrelationId;
using EList.Common.Logger;
using Newtonsoft.Json;
using NLog;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;
using ConfigurationManager = EList.Common.Configuration.ConfigurationManager;

namespace EList.Filestorage.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        public const string CorrelationIdHeaderName = "X-Correlation-Id";

        #region logger
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(LogManager.GetCurrentClassLogger());
        private const string LOGGER_NAME = "EList.Filestorage.Api.Middleware.ErrorHandlingMiddleware.";
        #endregion

        private readonly RequestDelegate next;
        private readonly ICorrelationIdProvider correlationIdProvider;
        private readonly IHostEnvironment _environment;

        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ICorrelationIdProvider correlationIdProvider,
            IHostEnvironment environment)
        {
            this.next = next;
            this.correlationIdProvider = correlationIdProvider;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            #region logger
            var correlationId = correlationIdProvider.Get();
            var METHOD_NAME = LOGGER_NAME + nameof(InvokeAsync);

            logger.Debug(correlationId, null, METHOD_NAME, $"{nameof(InvokeAsync)} method has started");
            #endregion

            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                #region logger
                logger.Error(correlationId, null, METHOD_NAME, $"Failed to call {METHOD_NAME}(): {exception.Message}", null, exception, null);
                #endregion
                await HandleExceptionAsync(context, exception);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var code = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;

            var correlationId = correlationIdProvider.Get();
            var exposeDetails = ShouldExposeDetailedErrors(_environment);

            if (!string.IsNullOrWhiteSpace(correlationId)
                && !context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }

            object bodyPayload = exposeDetails
                ? new
                {
                    errorCode = 1,
                    success = false,
                    message = FormatExceptionChain(exception),
                    stackTrace = exception.ToString(),
                    correlationId
                }
                : new
                {
                    errorCode = 1,
                    success = false,
                    message = "Внутренняя ошибка сервера. Обратитесь в поддержку и укажите correlation id.",
                    correlationId
                };

            return context.Response.WriteAsync(JsonConvert.SerializeObject(bodyPayload));
        }

        internal static bool ShouldExposeDetailedErrors(IHostEnvironment environment)
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Staging"))
                return true;

            if (!ConfigurationManager.AppSettings.Contains("features:exposeDetailedErrors"))
                return false;

            return bool.TryParse(
                       ConfigurationManager.AppSettings["features:exposeDetailedErrors"],
                       out var enabled)
                   && enabled;
        }

        private static string FormatExceptionChain(Exception exception)
        {
            var sb = new StringBuilder();
            var current = exception;
            var level = 0;
            while (current != null)
            {
                if (level == 0)
                    sb.AppendLine($"{current.GetType().Name}: {current.Message}");
                else
                    sb.AppendLine($"  caused by [{level}] {current.GetType().Name}: {current.Message}");

                current = current.InnerException;
                level++;
            }

            return sb.ToString().TrimEnd();
        }
    }
}
