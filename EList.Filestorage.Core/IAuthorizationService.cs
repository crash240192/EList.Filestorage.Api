using EList.Common.Models;
using EList.Filestorage.Model.Authorization;

namespace EList.Filestorage.Core
{
    public interface IAuthorizationService
    {
        Task<CommandResult> CreateOrActivateAsync(AuthorizationDataRequest authData);
        Task<CommandResult> DisableAsync(AuthorizationDataRequest authData);
        Task<CommandResult<AuthorizationData>> GetAsync(AuthorizationDataRequest authData);
    }
}
