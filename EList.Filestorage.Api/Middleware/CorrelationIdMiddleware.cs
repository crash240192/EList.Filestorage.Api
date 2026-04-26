using EList.Common.Constants;

namespace EList.Filestorage.Api.Middleware
{
    public class CorrelationIdMiddleware
    {
        private RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Items.ContainsKey(Constants.HttpHeaderKeys.CORRELATION_ID))
            {
                context.Items.Add(Constants.HttpHeaderKeys.CORRELATION_ID, Guid.NewGuid().ToString());
            }

            await _next.Invoke(context);
        }
    }
}
