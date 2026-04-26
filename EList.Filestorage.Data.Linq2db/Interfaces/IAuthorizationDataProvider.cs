using EList.Filestorage.Data.Linq2db.Dto;

namespace EList.Filestorage.Data.Linq2db.Interfaces
{
    public interface IAuthorizationDataProvider
    {
        void Configure(string connectionStringName);
        Task CreateOrActivateAsync(AuthorizationDataDto authData);
        Task DisableAsync(Guid token, string jwtHash);
        Task<AuthorizationDataDto> GetAsync(Guid token, string jwtHash);
    }
}
