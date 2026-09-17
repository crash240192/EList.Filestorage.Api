using EList.Filestorage.Data.Linq2db.Interfaces;

namespace EList.Filestorage.Core.Impl
{
    public class AuthorizationDataStorage : IAuthorizationDataStorage
    {
        private readonly IAuthorizationDataProvider _authorizationDataProvider;

        public Guid Token { get; private set; }
        public string JwtHash { get; private set; }
        public Guid? AccoutId { get; private set; }
        public bool IsServiceRequest { get; private set; }

        public AuthorizationDataStorage(IAuthorizationDataProvider authorizationDataProvider)
        {
            _authorizationDataProvider = authorizationDataProvider;
        }

        public async Task SetAuthorizationData(Guid token, string jwtHash)
        {
            Token = token;
            JwtHash = jwtHash;
            IsServiceRequest = false;
            AccoutId = (await _authorizationDataProvider.GetAsync(token, jwtHash))?.AccountId;
        }

        public void SetServiceRequest()
        {
            Token = Guid.Empty;
            JwtHash = string.Empty;
            AccoutId = null;
            IsServiceRequest = true;
        }
    }
}
