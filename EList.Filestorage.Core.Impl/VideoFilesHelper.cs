using System.Diagnostics;
using System.Globalization;

namespace EList.Filestorage.Core.Impl
{
    public static class VideoFilesHelper
    {
        /// <summary>
        /// Извлекает один кадр из видео-потока в JPEG через ffmpeg (stdin/stdout, без временных файлов).
        /// </summary>
        /// <param name="videoStream">Поток с видео; при возможности позиция будет сброшена в 0.</param>
        /// <param name="ffmpegExecutable">Имя или полный путь к ffmpeg (из PATH или из конфигурации).</param>
        /// <param name="position">Момент кадра; по умолчанию 1 с от начала.</param>
        /// <param name="timeout">Таймаут ожидания ffmpeg.</param>
        /// <param name="cancellationToken">Внешняя отмена.</param>
        public static async Task<byte[]> ExtractThumbnailToBytesAsync(
            Stream videoStream,
            string? ffmpegExecutable,
            TimeSpan? position = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (videoStream == null)
                throw new ArgumentNullException(nameof(videoStream));

            var exe = string.IsNullOrWhiteSpace(ffmpegExecutable) ? "ffmpeg" : ffmpegExecutable.Trim();
            if (videoStream.CanSeek)
                videoStream.Position = 0;

            var ts = (position ?? TimeSpan.FromSeconds(1))
                .ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

            var waitTimeout = timeout ?? TimeSpan.FromMinutes(2);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(waitTimeout);

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments =
                    "-hide_banner -loglevel error " +
                    $"-ss {ts} -i pipe:0 " +
                    "-frames:v 1 -f image2pipe -vcodec mjpeg pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            if (!process.Start())
                throw new InvalidOperationException("Не удалось запустить процесс ffmpeg.");

            var stderrTask = process.StandardError.ReadToEndAsync();

            var inputTask = Task.Run(async () =>
            {
                try
                {
                    await videoStream.CopyToAsync(process.StandardInput.BaseStream, 81920, timeoutCts.Token)
                        .ConfigureAwait(false);
                    await process.StandardInput.BaseStream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    process.StandardInput.Close();
                }
            }, CancellationToken.None);

            await using var outputMs = new MemoryStream();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputMs, timeoutCts.Token);

            try
            {
                await Task.WhenAll(inputTask, outputTask).ConfigureAwait(false);
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new TimeoutException($"Превышено время ожидания ffmpeg ({waitTimeout}).");
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg завершился с кодом {process.ExitCode}. {stderr}".Trim());

            var bytes = outputMs.ToArray();
            if (bytes.Length == 0)
                throw new InvalidOperationException(
                    $"ffmpeg не вернул данные кадра. {stderr}".Trim());

            return bytes;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process.HasExited)
                    return;
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
