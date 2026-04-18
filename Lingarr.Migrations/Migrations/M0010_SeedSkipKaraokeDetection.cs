using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(10)]
public class M0010_SeedSkipKaraokeDetection : Migration
{
    public override void Up()
    {
        Insert.IntoTable("settings").Row(new { key = "skip_karaoke_detection", value = "false" });
    }

    public override void Down()
    {
        Delete.FromTable("settings").Row(new { key = "skip_karaoke_detection" });
    }
}
