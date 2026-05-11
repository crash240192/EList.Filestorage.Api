using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Filestorage.Data.Linq2db.Interfaces;
using NLog;
using System.Diagnostics;

namespace EList.Filestorage.Core.Impl
{
    public class FileRepository : IFileRepository
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Core.Impl.FileRepository.";
        #endregion

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IDBStorageDataProvider _dbStorage;
        private readonly ILocalFileStorage _localStorage;
        private readonly bool useDbStorage;

        public FileRepository(ICorrelationIdProvider correlationIdProvider,
            IDBStorageDataProvider dbStorage,
            ILocalFileStorage localStorage)
        {
            _correlationIdProvider = correlationIdProvider;
            _dbStorage = dbStorage;
            _localStorage = localStorage;

            useDbStorage = ConfigurationManager.AppSettings.Contains("useDbStorage")
                ? bool.Parse(ConfigurationManager.AppSettings["useDbStorage"])
                : false;
        }

        public async Task<string> SaveAsync(Guid id, Stream stream)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(SaveAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");

            //if (useDbStorage)
            //{
            //    await _dbStorage.SaveAsync(id, stream);
            //}
            //else
            //{
                var result = await _localStorage.SaveAsync(id, stream);
            //}

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return result;
        }


        public async Task<Stream?> LoadAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(LoadAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");

            Stream resultStream = null;

            if (_localStorage.CheckFileExists(id))
                resultStream = _localStorage.Load(id);
            else 
                throw new Exception($"Файл '{id}' не найден в локальном хранилище");

            //if (resultStream == null && await _dbStorage.CheckFileExistAsync(id))
            //    resultStream = await _dbStorage.LoadAsync(id);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return resultStream;
        }

        public async Task DeleteAsync(Guid id)
        { 
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(DeleteAsync)}";
            logger.Debug(correlationId, null, methodName, "Method started");

            //if (await _dbStorage.CheckFileExistAsync(id))
            //    await _dbStorage.DeleteAsync(id);

            if (_localStorage.CheckFileExists(id))
                _localStorage.Delete(id);
            else
                throw new Exception($"Файл '{id}' не найден в локальном хранилище");

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
        }

        public async Task<bool> CheckFileExistsAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(DeleteAsync)}";
            logger.Debug(correlationId, null, methodName, null, "Method started");

            //var result = await _dbStorage.CheckFileExistAsync(id) || _localStorage.CheckFileExists(id);
            var result = _localStorage.CheckFileExists(id);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return result;
        }
    }
}
