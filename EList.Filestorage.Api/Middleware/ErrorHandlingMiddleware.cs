using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using Newtonsoft.Json;
using NLog;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace EList.Filestorage.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
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

            var isDevelopment = _environment.IsDevelopment();
            object bodyPayload = isDevelopment
                ? new
                {
                    errorCode = 1,
                    success = false,
                    message = FormatExceptionChain(exception),
                    stackTrace = exception.ToString()
                }
                : new
                {
                    errorCode = 1,
                    success = false,
                    message = "Внутренняя ошибка сервера. Обратитесь в поддержку и укажите correlation id.",
                    correlationId = correlationIdProvider.Get()
                };

            return context.Response.WriteAsync(JsonConvert.SerializeObject(bodyPayload));
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
