namespace EList.Filestorage.Model.Files
{
    /// <summary>
    /// Дополнительная информация по файлу (контекст)
    /// </summary>
    public class FileContext
    {
        /// <summary>
        /// Контекст (JSON-строка)
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Опционально обновить visibility вместе с контекстом
        /// </summary>
        public FileVisibility? Visibility { get; set; }
    }
}
