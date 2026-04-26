using LinqToDB;
using LinqToDB.Data;
using EList.Filestorage.Data.Linq2db.Dto;
using EList.DbDataProvider;

namespace EList.Filestorage.Data.Linq2db
{
    public class StorageDataConnection : DataConnection
    {
        public StorageDataConnection(string connectionName) : base(connectionName) { }

        public static void Configure(string[] connectionNames)
        {
            DefaultSettings = new ElistLinq2dbSettings(connectionNames);
        }

        public ITable<FileInfoDto> FileInfo => this.GetTable<FileInfoDto>();
        public ITable<FileDataDto> Files => this.GetTable<FileDataDto>();
        public ITable<AuthorizationDataDto> Auth => this.GetTable<AuthorizationDataDto>();
    }
}
