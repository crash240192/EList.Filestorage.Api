using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Threading;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Core;
using FluentScheduler;
using NLog;
using ILogger = NLog.ILogger;

namespace EList.Filestorage.BackgroundWorker.Impl
{
    /// <summary>
    /// Local hygiene: mark missing blobs unavailable; optionally delete disk files with no file_info.
    /// Cross-DB orphan purge (no refs in elist.api) is driven by elist.api OrphanFileGcWorker.
    /// </summary>
    public class BackgroundWorkerService : IBackgroundWorkerService
    {
        #region private readonly & constructor

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.BackgroundWorkerService.Impl.";

        private bool _isStarted;

        private readonly int _maxThreads;
        private readonly int _processIntervalMinutes;
        private readonly int _diskScanMax;
        private readonly bool _deleteDiskOrphans;
        private bool _active;
        public bool Active { get { return _active; } }

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IFileRepository _fileRepository;
        private readonly IFileInfoDataProvider _fileInfoDataProvider;
        private readonly ILocalFileStorage _localFileStorage;

        public BackgroundWorkerService(
            IFileInfoDataProvider storageDataProvider,
            IFileRepository fileRepository,
            ILocalFileStorage localFileStorage)
        {
            _correlationIdProvider = new RandomCorrelationIdProvider();
            _fileInfoDataProvider = storageDataProvider;
            _fileRepository = fileRepository;
            _localFileStorage = localFileStorage;

            var methodName = $"{LOGGER_NAME}ctor";
            var correlationId = _correlationIdProvider.Get();

            if (!int.TryParse(ConfigurationManager.AppSettings["BackgroundWorker:processIntervalMinutes"], out _processIntervalMinutes)
                || _processIntervalMinutes <= 0)
                _processIntervalMinutes = 15;
            logger.Info(correlationId, null, methodName, null, $"processIntervalMinutes = {_processIntervalMinutes}", null);

            if (!int.TryParse(ConfigurationManager.AppSettings["BackgroundWorker:maxThreads"], out _maxThreads)
                || _maxThreads <= 0)
                _maxThreads = 1;
            logger.Info(correlationId, null, methodName, null, $"maxThreads = {_maxThreads}", null);

            if (!int.TryParse(ConfigurationManager.AppSettings["BackgroundWorker:diskScanMax"], out _diskScanMax)
                || _diskScanMax <= 0)
                _diskScanMax = 500;

            _deleteDiskOrphans = ConfigurationManager.AppSettings.Contains("BackgroundWorker:deleteDiskOrphans")
                && bool.Parse(ConfigurationManager.AppSettings["BackgroundWorker:deleteDiskOrphans"]);

            if (ConfigurationManager.AppSettings.Contains("BackgroundWorker:active"))
                _active = bool.Parse(ConfigurationManager.AppSettings["BackgroundWorker:active"]);
            else
                _active = false;

            JobManager.JobException += (obj) =>
            {
                logger.Error(correlationId, null, methodName,
                    $"BackgroundWorker JobManager error: {obj.Exception.Message}", null, obj.Exception, null);
            };
        }

        #endregion

        public void ManualStart()
        {
            _active = true;
            Start();
        }

        public void ManualStop()
        {
            _active = false;
            Stop();
        }

        public void Start()
        {
            var methodName = $"{LOGGER_NAME}{nameof(Start)}";
            var correlationId = _correlationIdProvider.Get();

            try
            {
                logger.Info(correlationId, null, methodName, null, "Start method started", null);

                if (!_active)
                    return;

                if (_isStarted)
                    throw new InvalidOperationException("BackgroundWorker is already started");

                _isStarted = true;

                var reg = new Registry();
                var schedule = reg.Schedule(Process);
                schedule.ToRunEvery(_processIntervalMinutes).Minutes();
                JobManager.Initialize(reg);
                JobManager.Start();

                logger.Info(correlationId, null, methodName, null, "Start method finished", null);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName,
                    $"Failed to start BackgroundWorker: {ex.Message}", null, ex);
            }
        }

        public void Stop()
        {
            var methodName = $"{LOGGER_NAME}{nameof(Stop)}";
            var correlationId = _correlationIdProvider.Get();

            try
            {
                logger.Info(correlationId, null, methodName, null, "Stop method started", null);

                JobManager.Stop();
                _isStarted = false;

                logger.Info(correlationId, null, methodName, null, "Stop method finished", null);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName,
                    $"Failed to stop BackgroundWorker: {ex.Message}", null, ex);
            }
        }

        public void Process()
        {
            var methodName = $"{LOGGER_NAME}{nameof(Process)}";
            var correlationId = _correlationIdProvider.Get();

            try
            {
                logger.Debug(correlationId, null, methodName, null, "Process method tick");

                ReconcileMissingBlobs(correlationId, methodName);
                if (_deleteDiskOrphans)
                    ReconcileDiskOrphans(correlationId, methodName);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName,
                    $"Failed to process BackgroundWorker tick: {ex.Message}", null, ex);
            }
        }

        /// <summary>file_info says available but blob gone → mark IsAvailable=false.</summary>
        private void ReconcileMissingBlobs(string correlationId, string methodName)
        {
            var batch = AsyncHelper.RunSync(() =>
                _fileInfoDataProvider.GetOldestAvailableLocalFileInfosAsync(_maxThreads > 0 ? _maxThreads * 20 : 100));
            if (batch == null || batch.Count == 0)
                return;

            var marked = 0;
            foreach (var fileInfo in batch)
            {
                try
                {
                    var exists = AsyncHelper.RunSync(() => _fileRepository.CheckFileExistsAsync(fileInfo.Id));
                    if (exists)
                        continue;

                    fileInfo.IsAvailable = false;
                    fileInfo.Processing = false;
                    AsyncHelper.RunSync(() => _fileInfoDataProvider.UpdateAsync(fileInfo));
                    marked++;
                }
                catch (Exception ex)
                {
                    logger.Warn(correlationId, null, methodName,
                        $"Missing-blob check failed for {fileInfo.Id}: {ex.Message}");
                }
            }

            if (marked > 0)
                logger.Info(correlationId, null, methodName, null, $"Marked unavailable (missing blob): {marked}", null);
        }

        /// <summary>Disk file with no file_info row → delete (optional, capped scan).</summary>
        private void ReconcileDiskOrphans(string correlationId, string methodName)
        {
            var diskIds = _localFileStorage.EnumerateStoredIds(_diskScanMax);
            if (diskIds.Count == 0)
                return;

            var known = AsyncHelper.RunSync(() => _fileInfoDataProvider.GetListAsync(diskIds.ToList()));
            var knownSet = new HashSet<Guid>(known.Select(k => k.Id));
            var deleted = 0;

            foreach (var id in diskIds)
            {
                if (knownSet.Contains(id))
                    continue;

                try
                {
                    _localFileStorage.Delete(id);
                    deleted++;
                }
                catch (Exception ex)
                {
                    logger.Warn(correlationId, null, methodName,
                        $"Disk orphan delete failed for {id}: {ex.Message}");
                }
            }

            if (deleted > 0)
                logger.Info(correlationId, null, methodName, null, $"Deleted disk orphans: {deleted}", null);
        }
    }
}
