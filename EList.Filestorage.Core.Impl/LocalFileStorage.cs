using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using NLog;
using System.Diagnostics;

namespace EList.Filestorage.Core.Impl
{
    public class LocalFileStorage : ILocalFileStorage
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Core.Impl.LocalFileStorageService.";
        #endregion

        private string _storageDirectory;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        public LocalFileStorage(ICorrelationIdProvider correlationIdProvider)
        {
            _storageDirectory = Environment.GetEnvironmentVariable("STORAGE_PATH")
                ?? ConfigurationManager.AppSettings["storagePath"];
            _correlationIdProvider = correlationIdProvider;
        }

        public Stream Load(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(Load)}";

            var filePath = GetFilePath(id);

            if (!CheckFileExists(id))
                throw new Exception($"Файл '{id}' не найден в локальном хранилище");

            var fileStream = File.OpenRead(filePath);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return fileStream;
        }

        public async Task SaveAsync(Guid id, Stream stream)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(SaveAsync)}";

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
                //throw new Exception($"Не удалось найти директорию файлового хранилища: {_storageDirectory}");
            }

            stream.Position = 0;
            using (var fileStream = File.OpenWrite(GetFilePath(id)))
            {
                await stream.CopyToAsync(fileStream);
            }

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
        }

        public void Delete(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(Delete)}";

            if (!CheckFileExists(id))
                throw new Exception($"Файл с id='{id}' не найден в локальном хранилище");

            File.Delete(GetFilePath(id));

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
        }

        public bool CheckFileExists(Guid id)
        {
            return File.Exists(GetFilePath(id));
        }

        private string GetFilePath(Guid id)
        {
            var path = Path.Combine(_storageDirectory, id.ToString());
            return path;
        }
    }
}

