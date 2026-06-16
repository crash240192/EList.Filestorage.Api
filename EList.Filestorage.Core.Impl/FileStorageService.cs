using EList.Common.Configuration;
using EList.Common.CorrelationId;
using EList.Common.Logger;
using EList.Common.Models;
using EList.Common.Support;
using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Model.Files;
using FileTypeValidator.Infrastructure.Interfaces;

//using FileTypeChecker;
//using FileTypeChecker.Abstracts;
//using FileTypeChecker.Web.Attributes;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NLog;
using System.Diagnostics;
using System.IO;
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
        //private readonly IFileTypeValidator _fileTypeValidator;
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
        private readonly int photoPreviewScalePercent;
        private readonly int videoPreviewScalePercent;
        private readonly int videoTimeframeSeconds;
        private readonly int widthThreshold;
        private readonly int heightThreshold;

        public FileStorageService(ICorrelationIdProvider correlationIdProvider,
            IFileInfoDataProvider fileInfoDataProvider,
            IFileRepository fileRepository,
            IAuthorizationDataStorage authorizationDataStorage
            //, IFileTypeValidator fileTypeValidator
            )
        {
            _correlationIdProvider = correlationIdProvider;
            _storageDataProvider = fileInfoDataProvider;
            _fileRepository = fileRepository;
            _authorizationDataStorage = authorizationDataStorage;
            //_fileTypeValidator = fileTypeValidator;
            //_xdsClient = xdsClient;

            serviceUrl = ConfigurationManager.AppSettings["localServiceUrl"];
            maxFileSize = ConfigurationManager.AppSettings.Contains("maxFileSize") ? int.Parse(ConfigurationManager.AppSettings["maxFileSize"]) : null;
            useDbStorage = ConfigurationManager.AppSettings.Contains("useDbStorage")
               ? bool.Parse(ConfigurationManager.AppSettings["useDbStorage"])
               : false;

            photoPreviewScalePercent = ConfigurationManager.AppSettings.Contains("preview:photoScalePercent")
            ? Int32.Parse(ConfigurationManager.AppSettings["preview:photoScalePercent"])
            : 20;
            videoPreviewScalePercent = ConfigurationManager.AppSettings.Contains("preview:videoScalePercent")
            ? Int32.Parse(ConfigurationManager.AppSettings["preview:videoScalePercent"])
            : 15;
            videoTimeframeSeconds = ConfigurationManager.AppSettings.Contains("preview:videoTimeFrameSeconds")
            ? Int32.Parse(ConfigurationManager.AppSettings["preview:videoTimeFrameSeconds"])
            : 1;

            widthThreshold = ConfigurationManager.AppSettings.Contains("preview:threshold:width")
            ? Int32.Parse(ConfigurationManager.AppSettings["preview:threshold:width"])
            : 320;
            heightThreshold = ConfigurationManager.AppSettings.Contains("preview:threshold:height")
            ? Int32.Parse(ConfigurationManager.AppSettings["preview:threshold:height"])
            : 240;
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

        public async Task<CommandResult<UploadFileResult>> SaveFileAsync(string fileName, Stream fileStream, long? contentLength = null)
        {
            var correlationId = _correlationIdProvider.Get();
            var execTime = Stopwatch.StartNew();
            var methodName = $"{LOGGER_NAME}{nameof(SaveFileAsync)}";

            UploadFileResult result = null;

            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));

            if ((contentLength ?? fileStream.Length) == 0)
                return CommandResult<UploadFileResult>.Fail(1, $"Файл пуст.");

            if (maxFileSize != null)
            {
                if ((contentLength ?? fileStream.Length) > maxFileSize * 1024 * 1024)
                    return CommandResult<UploadFileResult>.Fail(1, $"Превышено ограничение ({maxFileSize} Мб) на размер загружаемого файла");
            }

            //TODO: Добавить проверку на соответствие передаваемого типа и mime
            var mimeType = await MimeTypeUtility.GetMimeTypeFromFileAsync(fileStream);
            fileStream.Position = 0;
            var isImage = MimeTypeUtility.IsImage(mimeType);
            var isVideo = !isImage ? MimeTypeUtility.IsVideo(mimeType) : false;

            if (!isVideo && !isImage)
                return CommandResult<UploadFileResult>.Fail(1, $"Допускается загрузка только фотографий и видео");

            #region fileChecker
            //if (!(await FileTypeChecker.FileTypeValidator.IsTypeRecognizableAsync(file)))
            //    return CommandResult<UploadFileResult>.Fail(1, $"Не удалось определить тип загружаемого файла");
            //file.Position = 0;

            //var fileMetadata = await FileTypeChecker.FileTypeValidator.GetFileTypeAsync(file);
            //file.Position = 0;

            //if (string.IsNullOrWhiteSpace(fileMetadata.Name))
            //    return CommandResult<UploadFileResult>.Fail(1, "Название прикрепляемого файла не должно быть пустым");            

            //if (string.IsNullOrEmpty(fileMetadata.Extension))
            //    return CommandResult<UploadFileResult>.Fail(1, "Прикрепляемый файл не может быть загружен без расширения");
            #endregion

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


            fileStream.Position = 0;
            var hash = Md5Helper.GetHash(fileStream);
            if (isImage)
            {
                #region photo preview
                FileInfoDto? previewDbItem = null;
                Stream? previewStream = null;
                var size = ImageScaleHelper.GetImageSize(fileStream);
                if (size.Width > widthThreshold || size.Height > heightThreshold)
                {
                    previewStream = ImageScaleHelper.ResizeImageByPercent(fileStream, widthThreshold, heightThreshold);
                    previewDbItem = await _storageDataProvider.CreateAsync(new FileInfoDto
                    {
                        ContentType = mimeType,
                        Extension = extension,
                        Filename = $"preview_{resultFileName}",
                        Size = previewStream.Length,
                        StorageType = StorageTypes.Local,
                        Processing = true,                        
                        Hash = hash,
                        AccountId = _authorizationDataStorage.AccoutId
                    });
                }
                #endregion

                #region save photo 
                fileStream.Position = 0;
                var photoDbItem = await _storageDataProvider.CreateAsync(new FileInfoDto
                {
                    ContentType = mimeType,
                    PreviewId = previewDbItem?.Id,
                    Extension = extension,
                    Filename = resultFileName,
                    Size = contentLength ?? fileStream.Length,
                    StorageType = useDbStorage ? StorageTypes.Db : StorageTypes.Local,
                    Processing = true,
                    Hash = hash,
                    AccountId = _authorizationDataStorage.AccoutId
                });
                #endregion

                try
                {
                    if (previewDbItem != null)
                        await _fileRepository.SaveAsync(previewDbItem.Id, previewStream);
                    await _fileRepository.SaveAsync(photoDbItem.Id, fileStream);
                }
                catch
                {
                    await _storageDataProvider.DeleteAsync(photoDbItem.Id);
                    if (previewDbItem?.Id != null)
                        await _storageDataProvider.DeleteAsync(previewDbItem.Id);
                    throw;
                }

                photoDbItem.Processing = false;
                photoDbItem.IsAvailable = true;
                await _storageDataProvider.UpdateAsync(photoDbItem);

                if (previewDbItem!=null)
                {
                    previewDbItem.Processing = false;
                    previewDbItem.IsAvailable = true;
                    await _storageDataProvider.UpdateAsync(previewDbItem);
                }
                
                result = new UploadFileResult
                {
                    Id = photoDbItem.Id,
                    Url = $"{serviceUrl}/{DOWNLOAD_METHOD}{photoDbItem.Id}"
                };

                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return new CommandResult<UploadFileResult>(result);
            }
            else
            {
                fileStream.Position = 0;
                var videoDbItem = await _storageDataProvider.CreateAsync(new FileInfoDto
                {
                    ContentType = mimeType,
                    PreviewId = null,
                    Extension = extension,
                    Filename = resultFileName,
                    Size = contentLength ?? fileStream.Length,
                    StorageType = StorageTypes.Local,
                    Processing = true,
                    Hash = hash,
                    AccountId = _authorizationDataStorage.AccoutId
                });

                string filePath;
                try
                {
                    filePath = await _fileRepository.SaveAsync(videoDbItem.Id, fileStream);
                }
                catch (Exception ex)
                {
                    await _storageDataProvider.DeleteAsync(videoDbItem.Id);
                    throw new Exception($"Не удалось сохранить файл: {ex.Message}");
                }

                if (filePath == null)
                {
                    await _storageDataProvider.DeleteAsync(videoDbItem.Id);
                    throw new Exception("Не удалось сохранить файл");
                }

                var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
                var thumbnailStream = new MemoryStream();
                ffMpeg.GetVideoThumbnail(filePath, thumbnailStream, videoTimeframeSeconds);
                thumbnailStream.Position = 0;

                var scaledThumbnail = ImageScaleHelper.ResizeImageByPercent(thumbnailStream, widthThreshold, heightThreshold);
                if (thumbnailStream.Length == 0)
                {
                    logger.Warn(correlationId, null, methodName, $"Не удалось извлечь превью для видеофайла с id='{videoDbItem.Id}'");
                }
                else
                {
                    var thumbnailMimeType = await MimeTypeUtility.GetMimeTypeFromFileAsync(scaledThumbnail);
                    var thumbnailExtension = MimeTypeUtility.MimeTypeToFileExtension(thumbnailMimeType);
                    scaledThumbnail.Position = 0;

                    var thumbnailDbItem = await _storageDataProvider.CreateAsync(new FileInfoDto
                    {
                        ContentType = thumbnailMimeType,
                        Extension = thumbnailExtension,
                        Filename = $"preview_{resultFileName}",
                        Size = thumbnailStream.Length,
                        StorageType = StorageTypes.Local,
                        Processing = true,
                        Hash = hash,
                        AccountId = _authorizationDataStorage.AccoutId
                    });

                    try
                    {
                        await _fileRepository.SaveAsync(thumbnailDbItem.Id, scaledThumbnail);
                        
                        thumbnailDbItem.Processing = false;
                        thumbnailDbItem.IsAvailable = true;
                        await _storageDataProvider.UpdateAsync(thumbnailDbItem);
                        videoDbItem.PreviewId = thumbnailDbItem.Id;
                    }
                    catch (Exception ex)
                    {
                        await _storageDataProvider.DeleteAsync(thumbnailDbItem.Id);
                        logger.Warn(correlationId, null, methodName, $"Не удалось сохранить файл превью для видеофайла с id='{videoDbItem.Id}': {ex.Message}");
                    }
                }

                videoDbItem.Processing = false;
                videoDbItem.IsAvailable = true;
                await _storageDataProvider.UpdateAsync(videoDbItem);

                result = new UploadFileResult
                {
                    Id = videoDbItem.Id,
                    Url = $"{serviceUrl}/{DOWNLOAD_METHOD}{videoDbItem.Id}"
                };

                logger.Debug(correlationId, null, methodName, "Method finished", null, execTime.Elapsed);
                return new CommandResult<UploadFileResult>(result);
            }
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

        public async Task<FileStreamContainer> GetFileAsync(Guid id, bool? fullSize = false)
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

            var resultFileId = fullSize != null && fullSize.Value ? fileInfo.Id : fileInfo.PreviewId ?? fileInfo.Id;

            var fileStream = await _fileRepository.LoadAsync(resultFileId);
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