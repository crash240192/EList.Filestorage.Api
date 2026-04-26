namespace EList.Filestorage.Model.Authorization
{
    /// <summary>
    /// Контейнер авторизационных данных
    /// </summary>
    public class AuthorizationDataRequest
    {
        /// <summary>
        /// Токен eList
        /// </summary>
        public Guid Token { get; set; }

        /// <summary>
        /// Идентификатор аккаунта eList
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Хэш JWT
        /// </summary>
        public string JwtHash { get; set; }
    }
}
