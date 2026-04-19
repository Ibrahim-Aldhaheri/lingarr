using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(10)]
public class M0010_SeedKaraokeFilterEnabled : Migration
{
    public override void Up()
    {
        // Opt-in beta filter that skips translation for karaoke romaji styles and
        // ASS/SSA vector drawings. Default off so stock behaviour is unchanged.
        Insert.IntoTable("settings").Row(new { key = "karaoke_filter_enabled", value = "false" });
    }

    public override void Down()
    {
        Delete.FromTable("settings").Row(new { key = "karaoke_filter_enabled" });
    }
}
