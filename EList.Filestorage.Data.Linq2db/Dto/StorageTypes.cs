using LinqToDB.Mapping;

namespace EList.Filestorage.Data.Linq2db.Dto
{
    public enum StorageTypes
    {
        [MapValue(Value = "local")]
        Local = 0,
        
        [MapValue(Value = "db")]
        Db = 1
    }
}
