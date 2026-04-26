using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Common.Support;
using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Model.Files;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NLog;
using System.Diagnostics;
using FileInfo = EList.Filestorage.Model.Files.FileInfo;
using FileStreamContainer = EList.Filestorage.Model.Files.FileStreamContainer;

namespace EList.Filestorage.Core.Impl
{
    public class FileStorageService : IFileStorageService
    {
        #region logger
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly ILoggerWrapper logger = new NLogLoggerWrapper(log);
        private const string LOGGER_NAME = "EList.Filestorage.Core.Impl.FileStorageService.";
        #endregion

        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IFileInfoDataProvider _storageDataProvider;
        private readonly IFileRepository _fileRepository;
        private readonly IAuthorizationDataStorage _authorizationDataStorage;
        //private readonly IXDSStreamClient _xdsClient;

        private const string METADATA_TAG_HASH = "hash";
        private const string METADATA_TAG_SIZE = "size";
        private const string METADATA_TAG_STORAGE_TYPE = "storage_type";
        private const string METADATA_TAG_EXTENSION = "extension";
        private const string METADATA_TAG_IS_AVAILABLE = "is_available";
        private const string DOWNLOAD_METHOD = "download/";

        private readonly string serviceUrl;
        private long? maxFileSize;
        private readonly bool useDbStorage;

        public FileStorageService(ICorrelationIdProvider correlationIdProvider,
            IFileInfoDataProvider fileInfoDataProvider,
            IFileRepository fileRepository,
            IAuthorizationDataStorage authorizationDataStorage)
        {
            _correlationIdProvider = correlationIdProvider;
            _storageDataProvider = fileInfoDataProvider;
            _fileRepository = fileRepository;
            _authorizationDataStorage = authorizationDataStorage;
            //_xdsClient = xdsClient;

            serviceUrl = ConfigurationManager.AppSettings["localServiceUrl"];
            maxFileSize = ConfigurationManager.AppSettings.Contains("maxFileSize") ? int.Parse(ConfigurationManager.AppSettings["maxFileSize"]) : null;
            useDbStorage = ConfigurationManager.AppSettings.Contains("useDbStorage")
               ? bool.Parse(ConfigurationManager.AppSettings["useDbStorage"])
               : false;
        }

        public async Task<CommandResult<UploadFileResult>> SaveFileAsync(IFormFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            using (var stream = file.OpenReadStream())
            {
                return await SaveFileAsync(file.FileName, stream);
            }
        }

        public async Task<CommandResult<UploadFileResult>> SaveFileAsync(string fileName, Stream file, long? contentLength = null)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(SaveFileAsync)}";

            UploadFileResult result = null;

            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if ((contentLength ?? file.Length) == 0)
                return CommandResult<UploadFileResult>.Fail(1, $"Файл пуст.");

            if (maxFileSize != null)
            {
                if ((contentLength ?? file.Length) > maxFileSize * 1024 * 1024)
                    return CommandResult<UploadFileResult>.Fail(1, $"Превышено ограничение ({maxFileSize} Мб) на размер загружаемого файла.");
            }

            var extension = string.Empty;
            var resultFileName = string.Empty;
            var fileNameItems = fileName.Split('.');
            if (fileNameItems?.Length > 1)
            {
                extension = fileNameItems?.LastOrDefault();
                var filenameItemsWithoutExtension = fileNameItems?.ToList();
                if (fileNameItems?.Count() > 0)
                    filenameItemsWithoutExtension?.RemoveAt(fileNameItems.Length - 1);
                resultFileName = string.Join(".", filenameItemsWithoutExtension ?? new List<string>(0));
            }
            else
            {
                resultFileName = fileNameItems?.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(resultFileName))
                return CommandResult<UploadFileResult>.Fail(1, "Название прикрепляемого файла не должно быть пустым");

            if (string.IsNullOrEmpty(extension))
                return CommandResult<UploadFileResult>.Fail(1, "Прикрепляемый файл не может быть загружен без расширения (прим. .doc, .txt и т.д.)");

            var mimeType = MimeTypeUtility.FileExtensionToMimeType(extension);

            file.Position = 0;
            var hash = Md5Helper.GetHash(file);

            var newItem = await _storageDataProvider.CreateAsync(new FileInfoDto
            {
                ContentType = mimeType,
                Extension = extension,
                Filename = resultFileName,
                Size = contentLength ?? file.Length,
                StorageType = useDbStorage ? StorageTypes.Db : StorageTypes.Local,
                Processing = true,
                Hash = hash,
                AccountId = _authorizationDataStorage.AccoutId
            });

            try
            {
                await _fileRepository.SaveAsync(newItem.Id, file);
            }
            catch
            {
                await _storageDataProvider.DeleteAsync(newItem.Id);
                throw;
            }

            newItem.Processing = false;
            newItem.IsAvailable = true;
            await _storageDataProvider.UpdateAsync(newItem);

            result = new UploadFileResult
            {
                Id = newItem.Id,
                Url = $"{serviceUrl}/{DOWNLOAD_METHOD}{newItem.Id}"
            };


            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return new CommandResult<UploadFileResult>(result);
        }

        public async Task<CommandResult> AttachFileContextAsync(Guid fileId, FileContext fileContext)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(AttachFileContextAsync)}";

            if (fileContext == null)
                return CommandResult.Fail(1, "Поле 'fileContext' не должно быть пустым");

            if (string.IsNullOrWhiteSpace(fileContext.Context))
                return CommandResult.Fail(1, "Поле 'context' не должно быть пустым");

            try
            {
                var context = JObject.Parse(fileContext.Context);
            }
            catch
            {
                return CommandResult.Fail(1, "Значение поля 'fileContext' не удаётся привести к виду jObject");
            }

            var fileInfo = await _storageDataProvider.GetAsync(fileId);
            if (fileInfo == null)
                return CommandResult.Fail(1, $"Не удалось найти информацию о файле с 'id={fileId}'");

            fileInfo.Context = fileContext.Context;

            await _storageDataProvider.UpdateAsync(fileInfo);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return CommandResult.OK;
        }

        public async Task<FileStreamContainer> GetFileAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetFileAsync)}";

            var fileInfo = await _storageDataProvider.GetAsync(id);
            if (fileInfo == null)
                throw new NullReferenceException($"Файл с id='{id}' отсутствует или не найден");

            var isAvailable = fileInfo.IsAvailable;
            fileInfo.IsAvailable = await _fileRepository.CheckFileExistsAsync(id);

            if (isAvailable != fileInfo.IsAvailable)
                await _storageDataProvider.UpdateAsync(fileInfo);

            if (!fileInfo.IsAvailable)
                throw new NullReferenceException($"Файл с id='{id}' утерян");

            var fileStream = await _fileRepository.LoadAsync(fileInfo.Id);

            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);

            return new FileStreamContainer
            {
                Stream = fileStream,
                ContentType = fileInfo.ContentType,
                FileName = $"{fileInfo.Filename}.{fileInfo.Extension}"
            };

        }

        public async Task<CommandResult<Model.Files.FileInfo>> GetFileInfoAsync(Guid id)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(GetFileInfoAsync)}";

            var fileInfo = await _storageDataProvider.GetAsync(id);
            if (fileInfo == null)
                return CommandResult<Model.Files.FileInfo>.Fail(1, $"Не найдена информация о файле с id='{id}'");

            Model.Files.FileInfo result;

            var mimeType = MimeTypeUtility.FileExtensionToMimeType(fileInfo.Extension);

            var fileExists = await _fileRepository.CheckFileExistsAsync(id);

            if (fileExists != fileInfo.IsAvailable)
            {
                fileInfo.IsAvailable = fileExists;
                await _storageDataProvider.UpdateAsync(fileInfo);
            }

            result = new FileInfo
            {
                Id = fileInfo.Id,
                Title = fileInfo.Filename,
                Url = $"{serviceUrl}/{DOWNLOAD_METHOD}{fileInfo.Id}",
                MimeType = mimeType,
                Metadata = new List<Metadata>
                    {
                        new Metadata
                        {
                            Key = METADATA_TAG_SIZE,
                            Value = fileInfo.Size.ToString()
                        },
                        new Metadata
                        {
                            Key = METADATA_TAG_STORAGE_TYPE,
                            Value = fileInfo.StorageType.ToString()
                        },
                        new Metadata
                        {
                            Key = METADATA_TAG_EXTENSION,
                            Value = fileInfo.Extension
                        },
                        new Metadata
                        {
                            Key = METADATA_TAG_HASH,
                            Value = fileInfo.Hash
                        },
                        new Metadata
                        {
                            Key = METADATA_TAG_IS_AVAILABLE,
                            Value = fileExists.ToString()
                        }
                    }
            };


            logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
            return new CommandResult<Model.Files.FileInfo>(result);
        }
    }
}