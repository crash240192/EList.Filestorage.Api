using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Data.Linq2db;
using LinqToDB;

namespace EList.Filestorage.Data.Linq2db.Impl
{
    public class DBStorageDataProvider : IDBStorageDataProvider
    {
        private string connectionStringName = null;

        protected StorageDataConnection GetDataConnection()
        {
            return new StorageDataConnection(connectionStringName);
        }

        public void Configure(string connectionStringName)
        {
            this.connectionStringName = connectionStringName;
            StorageDataConnection.Configure(new[] { connectionStringName });
        }


        public async Task DeleteAsync(Guid id)
        {
            using (var connection = GetDataConnection())
            {
                var item = await connection.Files.Where(i => i.Id == id).DeleteAsync();
            }
        }

        public async Task<Stream> LoadAsync(Guid id)
        {
            using (var connection = GetDataConnection())
            {
                var item = await connection.Files.FirstOrDefaultAsync(i => i.Id == id);

                if (item == null)
                    throw new Exception($"Файл с id='{id}' не найден в базе данных");

                return new MemoryStream(item.Data);
            }
        }

        public async Task SaveAsync(Guid id, Stream stream)
        {
            using (var connection = GetDataConnection())
            {
                var newFile = new FileDataDto
                {
                    Id = id,
                    Data = ReadStream(stream)
                };

                await connection.InsertAsync(newFile);
            }
        }

        public async Task<bool> CheckFileExistAsync(Guid id)
        {
            using (var connection = GetDataConnection())
            {
                var result = await connection.Files.Where(i => i.Id == id).AnyAsync();
                return result;
            }
        }

        public static byte[] ReadStream(Stream stream)
        {
            stream.Position = 0;
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
