using FluentMigrator;

namespace EList.Filestorage.Database
{
    [Migration(2, "file-visibility")]
    public class M202609171955_FileVisibility : Migration
    {
        public override void Down() { }

        public override void Up() => Execute.EmbeddedScript("M202609171955_file_visibility.sql");
    }
}
