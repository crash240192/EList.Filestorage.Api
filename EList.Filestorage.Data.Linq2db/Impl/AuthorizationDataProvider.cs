using EList.Filestorage.Data.Linq2db.Dto;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Data.Linq2db;
using LinqToDB;

namespace EList.Filestorage.Data.Linq2db.Impl
{
    public class AuthorizationDataProvider : IAuthorizationDataProvider
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

        public async Task CreateOrActivateAsync(AuthorizationDataDto authData)
        {
            using (var db = GetDataConnection()) 
            {
                var existingItem = await db.Auth.FirstOrDefaultAsync(i => i.Token == authData.Token && i.JwtHash == authData.JwtHash);
                if (existingItem != null)
                {
                    existingItem.Active = true;
                    existingItem.UpdateDate = DateTimeOffset.Now;
                    await db.UpdateAsync(existingItem);
                }
                else
                { 
                    authData.UpdateDate = DateTimeOffset.Now;
                    authData.CreateDate = DateTimeOffset.Now;
                    authData.Active = true;
                    await db.InsertAsync(authData);
                }
            }
        }

        public async Task DisableAsync(Guid token, string jwtHash)
        {
            using(var db = GetDataConnection())
            {
                var existingItem = await db.Auth.FirstOrDefaultAsync(i => i.Token == token && i.JwtHash == jwtHash);
                if (existingItem != null)
                {
                    existingItem.Active = false;
                    existingItem.UpdateDate = DateTimeOffset.Now;
                    await db.UpdateAsync(existingItem);
                }
            }
        }

        public async Task<AuthorizationDataDto> GetAsync(Guid token, string jwtHash)
        {
            using (var db = GetDataConnection())
            {
                var existingItem = await db.Auth.FirstOrDefaultAsync(i => i.Token == token && i.JwtHash == jwtHash);
                return existingItem;
            }
        }
    }
}
