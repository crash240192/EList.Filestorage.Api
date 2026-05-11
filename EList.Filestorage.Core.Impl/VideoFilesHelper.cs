using System.Diagnostics;
using System.Globalization;

namespace EList.Filestorage.Core.Impl
{
    public static class VideoFilesHelper
    {
        /// <summary>
        /// Извлекает один кадр из видео-потока в JPEG через ffmpeg.
        /// Видео сначала копируется во временный файл, затем <c>ffmpeg -i</c> — так надёжно на Windows
        /// (передача большого файла в stdin процесса через <see cref="Process"/> часто обрывается с «канал закрыт»).
        /// Кадр по-прежнему читается из stdout в память, отдельный файл превью на диске не создаётся.
        /// </summary>
        /// <param name="videoStream">Поток с видео; для <see cref="Stream.CanSeek"/> позиция сбрасывается в 0 перед копированием.</param>
        /// <param name="ffmpegExecutable">Имя или полный путь к ffmpeg.</param>
        /// <param name="inputExtensionHint">Расширение контейнера (без точки), для временного входного файла.</param>
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

            if (videoStream.CanSeek)
            {
                if (videoStream.Length == 0)
                    throw new InvalidOperationException("Поток видео пуст (длина 0).");

                videoStream.Position = 0;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{ext}");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await videoStream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                }

                var written = new FileInfo(tempPath).Length;
                if (written == 0)
                    throw new InvalidOperationException(
                        "Во временный файл не записано ни одного байта: проверьте, что поток не был прочитан до конца до вызова (для не-seekable потоков нужен буфер запроса / копирование в MemoryStream до хэша).");

                return await RunFfmpegFileToStdoutJpegAsync(tempPath, exe, position, waitTimeout, cancellationToken)
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
            {
                var flat = agg.Flatten().InnerExceptions;
                return flat.Count == 1 ? flat[0] : agg;
            }

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
