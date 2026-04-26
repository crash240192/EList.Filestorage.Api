using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using Newtonsoft.Json;
using NLog;
using System.Net;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        /// <param name="correlationIdProvider"></param>
        public ErrorHandlingMiddleware(RequestDelegate next, ICorrelationIdProvider correlationIdProvider)
        {
            this.next = next;
            this.correlationIdProvider = correlationIdProvider;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
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

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var code = (int)HttpStatusCode.InternalServerError;
            var contentType = context.Request.ContentType ?? "application/json";
            context.Response.ContentType = contentType;
            context.Response.StatusCode = code;

            string body = JsonConvert.SerializeObject(
                CommandResult<object>.Fail(1, GetLowerLevelExceptionMessage(exception), exception.StackTrace)
            );

            return context.Response.WriteAsync(body);
        }

        private static string GetLowerLevelExceptionMessage(Exception ex)
        {
            if (ex?.InnerException != null)
                return GetLowerLevelExceptionMessage(ex.InnerException);

            return ex?.Message;
        }
    }
}
