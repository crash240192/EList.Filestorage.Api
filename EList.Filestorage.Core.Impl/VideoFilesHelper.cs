using System.Diagnostics;
using System.Globalization;

namespace EList.Filestorage.Core.Impl
{
    public static class VideoFilesHelper
    {
        /// <summary>
        /// Извлекает один кадр из видео-потока в JPEG через ffmpeg.
        /// Сначала пробует stdin/stdout без файла результата; при типичных сбоях pipe на Windows
        /// повторяет попытку с временным входным файлом (JPEG по-прежнему только в памяти).
        /// </summary>
        /// <param name="videoStream">Поток с видео.</param>
        /// <param name="ffmpegExecutable">Имя или полный путь к ffmpeg.</param>
        /// <param name="inputExtensionHint">Расширение контейнера (без точки), для временного файла при fallback.</param>
        /// <param name="position">Момент кадра; по умолчанию 1 с от начала.</param>
        /// <param name="timeout">Таймаут ожидания ffmpeg.</param>
        /// <param name="cancellationToken">Внешняя отмена.</param>
        public static async Task<byte[]> ExtractThumbnailToBytesAsync(
            Stream videoStream,
            string? ffmpegExecutable,
            string? inputExtensionHint = null,
            TimeSpan? position = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (videoStream == null)
                throw new ArgumentNullException(nameof(videoStream));

            var exe = string.IsNullOrWhiteSpace(ffmpegExecutable) ? "ffmpeg" : ffmpegExecutable.Trim();
            var ext = SanitizeExtension(inputExtensionHint);
            var waitTimeout = timeout ?? TimeSpan.FromMinutes(2);

            MemoryStream? ownedCopy = null;
            Stream workStream;
            if (!videoStream.CanSeek)
            {
                ownedCopy = new MemoryStream();
                await videoStream.CopyToAsync(ownedCopy, 81920, cancellationToken).ConfigureAwait(false);
                workStream = ownedCopy;
                workStream.Position = 0;
            }
            else
            {
                videoStream.Position = 0;
                workStream = videoStream;
            }

            try
            {
                try
                {
                    return await ExtractViaStdinPipeAsync(workStream, exe, position, waitTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ShouldRetryWithTempInputFile(ex))
                {
                    if (workStream.CanSeek)
                        workStream.Position = 0;

                    return await ExtractViaTempInputFileAsync(workStream, exe, ext, position, waitTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                ownedCopy?.Dispose();
            }
        }

        private static async Task<byte[]> ExtractViaStdinPipeAsync(
            Stream videoStream,
            string ffmpegExecutable,
            TimeSpan? position,
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            var ts = (position ?? TimeSpan.FromSeconds(1))
                .ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(waitTimeout);

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-probesize");
            psi.ArgumentList.Add("100M");
            psi.ArgumentList.Add("-analyzeduration");
            psi.ArgumentList.Add("100M");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("-");
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(ts);
            psi.ArgumentList.Add("-an");
            psi.ArgumentList.Add("-frames:v");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("image2pipe");
            psi.ArgumentList.Add("-vcodec");
            psi.ArgumentList.Add("mjpeg");
            psi.ArgumentList.Add("-");

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

            Exception? pipelineException = null;
            try
            {
                await Task.WhenAll(inputTask, outputTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                pipelineException = Unwrap(ex);
                // Не вызываем Kill: иначе второй параллельный поток часто получает «Канал был закрыт» вместо реальной причины.
            }

            try
            {
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

            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();

            if (pipelineException != null)
            {
                throw new InvalidOperationException(
                    $"Ошибка обмена данными с ffmpeg: {pipelineException.Message}" +
                    (string.IsNullOrEmpty(stderr) ? string.Empty : $". Вывод ffmpeg: {stderr}"),
                    pipelineException);
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg завершился с кодом {process.ExitCode}. {stderr}".Trim());

            var bytes = outputMs.ToArray();
            if (bytes.Length == 0)
                throw new InvalidOperationException(
                    $"ffmpeg не вернул данные кадра. {stderr}".Trim());

            return bytes;
        }

        private static async Task<byte[]> ExtractViaTempInputFileAsync(
            Stream videoStream,
            string ffmpegExecutable,
            string extension,
            TimeSpan? position,
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{extension}");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await videoStream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                }

                return await RunFfmpegFileToStdoutJpegAsync(tempPath, ffmpegExecutable, position, waitTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        private static async Task<byte[]> RunFfmpegFileToStdoutJpegAsync(
            string inputPath,
            string ffmpegExecutable,
            TimeSpan? position,
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            var ts = (position ?? TimeSpan.FromSeconds(1))
                .ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(waitTimeout);

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(ts);
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(inputPath);
            psi.ArgumentList.Add("-an");
            psi.ArgumentList.Add("-frames:v");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("image2pipe");
            psi.ArgumentList.Add("-vcodec");
            psi.ArgumentList.Add("mjpeg");
            psi.ArgumentList.Add("-");

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            if (!process.Start())
                throw new InvalidOperationException("Не удалось запустить процесс ffmpeg.");

            var stderrTask = process.StandardError.ReadToEndAsync();
            await using var outputMs = new MemoryStream();
            Exception? readEx = null;
            try
            {
                await process.StandardOutput.BaseStream.CopyToAsync(outputMs, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                readEx = Unwrap(ex);
            }

            try
            {
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

            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();

            if (readEx != null)
            {
                throw new InvalidOperationException(
                    $"Ошибка чтения кадра из ffmpeg: {readEx.Message}" +
                    (string.IsNullOrEmpty(stderr) ? string.Empty : $". Вывод ffmpeg: {stderr}"),
                    readEx);
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg завершился с кодом {process.ExitCode}. {stderr}".Trim());

            var bytes = outputMs.ToArray();
            if (bytes.Length == 0)
                throw new InvalidOperationException(
                    $"ffmpeg не вернул данные кадра. {stderr}".Trim());

            return bytes;
        }

        private static bool ShouldRetryWithTempInputFile(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                if (e is TimeoutException or OperationCanceledException or OutOfMemoryException)
                    return false;

                if (e is IOException)
                    return true;

                var msg = e.Message;
                if (msg.Contains("Канал был закрыт", StringComparison.Ordinal))
                    return true;
                if (msg.Contains("broken pipe", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (msg.Contains("pipe has been ended", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (msg.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string SanitizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return "mp4";

            var ext = extension.Trim().TrimStart('.');
            if (ext.Length == 0)
                return "mp4";

            foreach (var c in ext)
            {
                if (!char.IsAsciiLetterOrDigit(c))
                    return "mp4";
            }

            return ext.Length > 16 ? ext[..16] : ext;
        }

        private static Exception Unwrap(Exception ex)
        {
            if (ex is AggregateException agg)
                return agg.Flatten().InnerExceptions.Count == 1
                    ? agg.Flatten().InnerExceptions[0]
                    : agg;

            return ex;
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

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
