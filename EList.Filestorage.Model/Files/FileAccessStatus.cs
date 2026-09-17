namespace EList.Filestorage.Model.Files
{
    /// <summary>
    /// Модерационный доступ к файлу (не путать с Visibility Public/Private).
    /// Active — обычные правила visibility; Blocked — только service-token (разбор жалоб).
    /// </summary>
    public enum FileAccessStatus : short
    {
        Active = 0,
        Blocked = 1
    }
}
