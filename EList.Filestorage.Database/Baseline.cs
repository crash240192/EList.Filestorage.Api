using FluentMigrator;

namespace EList.Filestorage.Database
{
    [Migration(1, "1.0.0.0")]
    public class Baseline : Migration
    {
        public override void Up()
        {
            Execute.EmbeddedScript("Baseline.sql");
        }

        public override void Down()
        {

        }
    }
}
