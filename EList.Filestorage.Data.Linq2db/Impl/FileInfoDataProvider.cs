using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Data.Linq2db;
using LinqToDB;

namespace EList.Filestorage.Data.Linq2db.Impl
{
    public class FileInfoDataProvider : IFileInfoDataProvider
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

        public async Task<FileInfoDto?> GetAsync(Guid id)
        {
            using (var db = GetDataConnection())
            {
                return await db.FileInfo.FirstOrDefaultAsync(x => x.Id == id);
            }
        }

        public async Task<List<FileInfoDto>> GetOldestAvailableLocalFileInfosAsync(int? take = 0)
        {
            using (var db = GetDataConnection())
            {
                var query = db.FileInfo.AsQueryable();

                // N3 legacy
                //    .Where(i => i.IsAvailable && !i.Processing && i.StorageType != StorageTypes.Xds);

                if (take > 0)
                    query = query.Take(take.Value);
                    
                query = query.OrderBy(i => i.UploadedAt);

                var result = await query.ToListAsync();
                return result;
            }
        }

        //public async Task<FileInfoDto?> GetByXdsIdAsync(Guid xdsId)
        //{
        //    using (var db = GetDataConnection())
        //    {
        //        return await db.FileInfo.FirstOrDefaultAsync(x => x.XdsId == xdsId);
        //    }
        //}

        public async Task<List<FileInfoDto>> GetListAsync(List<Guid> ids)
        {
            using (var db = GetDataConnection())
            {
                var result = await db.FileInfo.Where(i => ids.Contains(i.Id)).ToListAsync();
                return result;
            }
        }

        public async Task<FileInfoDto> CreateAsync(FileInfoDto item)
        {
            using (var db = GetDataConnection())
            {
                item.Processing = true;
                item.UploadedAt = DateTimeOffset.Now;
                var newId = (Guid)await db.InsertWithIdentityAsync(item);
                item.Id = newId;
                item.IsAvailable = true;
                return item;
            }
        }

        public async Task<FileInfoDto> UpdateAsync(FileInfoDto item)
        {
            using (var db = GetDataConnection())
            {
                var existingItem = await db.FileInfo.FirstOrDefaultAsync(i => i.Id == item.Id);

                if (existingItem == null)
                    throw new Exception($"Не найден файл с id='{item.Id}'");

                existingItem.Extension = item.Extension;
                existingItem.Filename = item.Filename;
                existingItem.Size = item.Size;
                existingItem.StorageType = item.StorageType;
                existingItem.UploadedAt = item.UploadedAt;
                existingItem.Context = item.Context;
                existingItem.AccountId = item.AccountId;
                existingItem.Processing = item.Processing;
                existingItem.IsAvailable = item.IsAvailable;
                await db.UpdateAsync(existingItem);

                return existingItem;
            }
        }

        public async Task UpdateFilePreviewIdAsync(Guid fileId, Guid previewId)
        {
            using (var db = GetDataConnection())
            {
                var existingItem = await db.FileInfo.FirstOrDefaultAsync(i => i.Id == fileId);

                if (existingItem == null)
                    throw new Exception($"Не найден файл с id='{fileId}'");

                existingItem.PreviewId = previewId;
                await db.UpdateAsync(existingItem);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            using (var db = GetDataConnection())
            {
                await db.FileInfo.DeleteAsync(i => i.Id == id);
            }
        }
    }
}
