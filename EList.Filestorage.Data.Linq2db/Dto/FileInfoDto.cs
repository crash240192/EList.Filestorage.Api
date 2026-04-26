using LinqToDB;
using LinqToDB.Mapping;

namespace EList.Filestorage.Data.Linq2db.Dto
{
    [Table("public.file_info")]
    public class FileInfoDto
    {
        [Column("id"), PrimaryKey, Identity]
        public Guid Id { get; set; }

        [Column("filename")]
        public string Filename { get; set; }

        [Column("extension")]
        public string Extension { get; set; }

        [Column("content_type")]
        public string ContentType { get; set; }

        [Column("size")]
        public long Size { get; set; }

        [Column("context", DataType = DataType.BinaryJson)]
        public string Context { get; set; }

        [Column("account_id")]
        public Guid? AccountId { get; set; }

        [Column("storage_type", DataType = DataType.Enum)]
        public StorageTypes StorageType { get; set; }

        [Column("hash")]
        public string Hash { get; set; }

        [Column("processing")]
        public bool Processing { get; set; }

        [Column("is_available")]
        public bool IsAvailable { get; set; }

        [Column("uploaded_at")]
        public DateTimeOffset UploadedAt { get; set; }
    }
}
