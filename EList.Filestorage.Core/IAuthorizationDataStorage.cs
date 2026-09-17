namespace EList.Filestorage.Core
{
    public interface IAuthorizationDataStorage
    {
        public Guid Token { get; }
        public string JwtHash { get; }
        public Guid? AccoutId { get; }
        /// <summary>True when request authenticated via elist.api service token.</summary>
        public bool IsServiceRequest { get; }

        Task SetAuthorizationData(Guid token, string jwtHash);
        void SetServiceRequest();
    }
}
