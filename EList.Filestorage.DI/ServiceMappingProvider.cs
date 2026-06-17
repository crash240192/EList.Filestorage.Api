using EList.Common.CorrelationId;
using EList.Common.DI;
using EList.Common.Encryption;
using EList.Filestorage.Data.Linq2db.Impl;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.BackgroundUploader;
using EList.Filestorage.BackgroundUploader.Impl;
using EList.Filestorage.Core;
using EList.Filestorage.Core.Impl;

namespace EList.Filestorage.DI
{
    public class ServiceMappingProvider : IServiceMappingProvider
    {
        public ServiceMapping GetServiceMapping()
        {
            var mapping = new ServiceMapping();

            // N3.Common mappings
            mapping.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
            mapping.AddSingleton<IEncryptionTool, EncryptionTool>();

            // DataProviders
            mapping.AddSingleton<IFileInfoDataProvider, FileInfoDataProvider>();
            //mapping.AddSingleton<IVersionDataProvider, VersionDataProvider>();
            mapping.AddSingleton<IDBStorageDataProvider, DBStorageDataProvider>();
            mapping.AddSingleton<IAuthorizationDataProvider, AuthorizationDataProvider>();

            // External clients
            //mapping.AddSingleton<IXDSStreamClient, XDSStreamClient>();
            mapping.AddSingleton<IAuthorizationDataStorage, AuthorizationDataStorage>();

            // Services
            mapping.AddSingleton<IBackgroundWorkerService, BackgroundWorkerService>();
            mapping.AddSingleton<IAuthorizationService, AuthorizationService>();
            mapping.AddSingleton<IFileStorageService, FileStorageService>();
            mapping.AddSingleton<ILocalFileStorage, LocalFileStorage>();

            // Repositories
            mapping.AddSingleton<IFileRepository, FileRepository>();
            
            return mapping;
        }
    }
}