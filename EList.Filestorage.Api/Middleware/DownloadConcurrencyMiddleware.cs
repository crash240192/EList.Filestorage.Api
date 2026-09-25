using Microsoft.AspNetCore.Http;
using ConfigurationManager = EList.Common.Configuration.ConfigurationManager;

namespace EList.Filestorage.Api.Middleware
{
    /// <summary>
    /// Ограничивает число одновременных download, чтобы при шторме превью
    /// (мозаики альбомов) процесс не уходил в перегрузку → 502/503 на reverse proxy.
    /// Лишние запросы ждут слот; при таймауте — 503 + Retry-After.
    /// </summary>
    public sealed class DownloadConcurrencyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SemaphoreSlim _slots;
        private readonly int _waitTimeoutMs;
        private readonly int _retryAfterSeconds;

        public DownloadConcurrencyMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            var max = ReadInt("downloadConcurrency:maxConcurrent", 32);
            if (max < 1) max = 1;
            _waitTimeoutMs = ReadInt("downloadConcurrency:waitTimeoutMs", 20_000);
            if (_waitTimeoutMs < 0) _waitTimeoutMs = 0;
            _retryAfterSeconds = Math.Max(1, ReadInt("downloadConcurrency:retryAfterSeconds", 2));
            _slots = new SemaphoreSlim(max, max);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!IsDownloadPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var acquired = await _slots.WaitAsync(_waitTimeoutMs, context.RequestAborted);
            if (!acquired)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers["Retry-After"] = _retryAfterSeconds.ToString();
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"Слишком много одновременных загрузок файлов. Повторите через мгновение.\"}");
                return;
            }

            try
            {
                await _next(context);
            }
            finally
            {
                _slots.Release();
            }
        }

        private static bool IsDownloadPath(PathString path)
        {
            var value = path.Value;
            if (string.IsNullOrEmpty(value)) return false;
            // pathBase уже срезан UsePathBase; остаётся /api/download/{id}
            return value.Contains("/download/", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(string key, int fallback)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var n))
                return n;
            return fallback;
        }
    }
}
