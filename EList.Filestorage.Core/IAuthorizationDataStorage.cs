namespace EList.Filestorage.Core
{
    public interface IAuthorizationDataStorage
    {
        public Guid Token { get; }
        public string JwtHash { get; }
        public Guid? AccoutId { get; }
        //Task<Guid?> GetAccountIdAsync(Guid token, string jwtHash);
        Task SetAuthorizationData(Guid token, string jwtHash);
    }
}
