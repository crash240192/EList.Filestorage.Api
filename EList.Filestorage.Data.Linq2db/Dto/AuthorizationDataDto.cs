using LinqToDB.Mapping;

namespace EList.Filestorage.Data.Linq2db.Dto
{
    [Table("public.authorization_data")]
    public class AuthorizationDataDto
    {
        [Column("token"), PrimaryKey]
        public Guid Token { get; set; }

        [Column ("account_id")]
        public Guid AccountId { get; set; }

        [Column("jwt_hash"), PrimaryKey]
        public string JwtHash { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        [Column("create_date")]
        public DateTimeOffset CreateDate { get; set; }

        [Column("update_date")]
        public DateTimeOffset UpdateDate { get; set;}
    }
}
