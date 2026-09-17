using FluentMigrator;

namespace EList.Filestorage.Database
{
    [Migration(3, "file-access-status")]
    public class M202609172033_FileAccessStatus : Migration
    {
        public override void Down() { }

        public override void Up() => Execute.EmbeddedScript("M202609172033_file_access_status.sql");
    }
}
