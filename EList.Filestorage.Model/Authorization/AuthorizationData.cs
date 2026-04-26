namespace EList.Filestorage.Model.Authorization
{
    /// <summary>
    /// Контейнер авторизационных данных
    /// </summary>
    public class AuthorizationData
    {
        /// <summary>
        /// Токен eList
        /// </summary>
        public Guid Token { get; set; }

        /// <summary>
        /// Идентификатор аккаунта eList
        /// </summary>
        public Guid AccountId { get; set; }

        /// <summary>
        /// Хэш JWT
        /// </summary>
        public string JwtHash { get; set; }

        /// <summary>
        /// Флаг активности
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTimeOffset CreateDate { get; set; }

        /// <summary>
        /// Дата последнего обновления
        /// </summary>
        public DateTimeOffset UpdateDate { get; set; }
    }
}
