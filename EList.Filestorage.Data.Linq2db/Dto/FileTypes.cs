using LinqToDB.Mapping;

namespace EList.Filestorage.Data.Linq2db.Dto
{
    public enum FileTypes
    {
        [MapValue(Value = "none")]
        None = 0,

        [MapValue(Value = "image")]
        Image = 1,
        
        [MapValue(Value = "video")]
        Video = 2        
    }
}
