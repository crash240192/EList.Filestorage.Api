using LinqToDB.Mapping;

namespace EList.Filestorage.Data.Linq2db.Dto
{
    [Table("public.file_data")]
    public class FileDataDto
    {
        [Column("id"), PrimaryKey]
        public Guid Id { get; set; }

        [Column("data", DataType = LinqToDB.DataType.Blob)]
        public byte[] Data { get; set; }
    }
}
