namespace EList.Filestorage.Model.Files
{
    /// <summary>
    /// Уровень доступности файла при download.
    /// Public — без логина; Private — только авторизованный пользователь или service-token.
    /// </summary>
    public enum FileVisibility : short
    {
        Public = 0,
        Private = 1
    }
}
