using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Threading;
using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Core;
using FluentScheduler;
using NLog;
using System.Collections.Concurrent;
using ILogger = NLog.ILogger;

namespace EList.Filestorage.BackgroundWorker.Impl
{
    public class BackgroundWorkerService : IBackgroundWorkerService
    {
        #region private readonly & constructor

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.BackgroundWorkerService.Impl.";

        private bool _isStarted;

        private readonly int _maxThreads;
        private readonly int _processIntervalMinutes;
        private static readonly object processSyncRoot = new object();
        private bool _active;
        public bool Active { get { return _active; } }

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IFileRepository _fileRepository;
        private readonly IFileInfoDataProvider _fileInfoDataProvider;
        //private readonly IXDSStreamClient _xdsClient;

        public BackgroundWorkerService(IFileInfoDataProvider storageDataProvider,
            IFileRepository fileRepository)
        {
            _correlationIdProvider = new RandomCorrelationIdProvider();
            _fileInfoDataProvider = storageDataProvider;
            _fileRepository = fileRepository;
            //_xdsClient = xdsClient;

            var methodName = $"{LOGGER_NAME}ctor";
            var correlationId = _correlationIdProvider.Get();

            bool timerIsParsed = int.TryParse(ConfigurationManager.AppSettings["backgroundUploader:processIntervalMinutes"], out _processIntervalMinutes);
            if (!timerIsParsed)
                _processIntervalMinutes = 15;
            logger.Info(correlationId, null, methodName, null, $"processIntervalMinutes = {_processIntervalMinutes}", null);

            var maxThreadsIsParsed = int.TryParse(ConfigurationManager.AppSettings["backgroundUploader:maxThreads"], out _maxThreads);
            if (!maxThreadsIsParsed)
                _maxThreads = 1;
            logger.Info(correlationId, null, methodName, null, $"maxThreads = {_maxThreads}", null);

            if (ConfigurationManager.AppSettings.Contains("backgroundUploader:active"))
                _active = bool.Parse(ConfigurationManager.AppSettings["backgroundUploader:active"]);
            else 
                _active = true;

            JobManager.JobException += (obj) =>
            {
                logger.Error(correlationId, null, methodName,
                    $"BackgroundUploader JobManager error: {obj.Exception.Message}", null, obj.Exception, null);
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
                    throw new InvalidOperationException("BackgroundUploader is already started");

                _isStarted = true;

                var reg = new Registry();
                var schedule = reg.Schedule(SendToXds);
                schedule.ToRunEvery(_processIntervalMinutes).Minutes();
                JobManager.Initialize(reg);
                JobManager.Start();

                logger.Info(correlationId, null, methodName, null, "Start method finished", null);
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName,
                    $"Failed to start BackgroundUploader: {ex.Message}", null, ex);
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
                    $"Failed to stop BackgroundUploader: {ex.Message}", null, ex);
            }
        }

        public void SendToXds()
        {
            var methodName = $"{LOGGER_NAME}{nameof(SendToXds)}";
            var correlationId = _correlationIdProvider.Get();

            try
            {
                logger.Debug(correlationId, null, methodName, null, "Process method tick");

                var availableFiles = AsyncHelper.RunSync(() => _fileInfoDataProvider.GetOldestAvailableLocalFileInfosAsync(_maxThreads));
                var queue = new ConcurrentQueue<FileInfoDto>(availableFiles);

                var action = new Action(() =>
                {
                    if (queue.IsEmpty)
                        return;

                    while (queue.TryDequeue(out FileInfoDto fileInfo))
                    {
                        try
                        {

                        }
                        catch (Exception ex)
                        {
                            logger.Error(correlationId, null, methodName,
                                    $"{nameof(SendToXds)} method has failed: {ex.Message}", null, ex);
                            throw;
                        }
                        finally
                        {
                            //_runningThreadsCount--;
                            if (fileInfo != null)
                            {
                                fileInfo.Processing = false;
                                AsyncHelper.RunSync(() => _fileInfoDataProvider.UpdateAsync(fileInfo));
                            }
                        }
                    }
                });

                var threadTasks = new List<Task>();
                for (var count = 0; count < _maxThreads; count++)
                {
                    var task = new Task(action);
                    threadTasks.Add(task);
                }

                threadTasks.ForEach(t => t.Start());

                Task.WaitAll(threadTasks.ToArray());
            }
            catch (Exception ex)
            {
                logger.Error(correlationId, null, methodName,
                    $"Failed to process BackgroundUploader tick: {ex.Message}", null, ex);
            }
        }
    }
}