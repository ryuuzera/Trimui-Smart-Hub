using TrimuiSmartHub.Core.Downloads;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Scraping;
using TrimuiSmartHub.Core.Storage;
using TrimuiSmartHub.Core.Sync;
using TrimuiSmartHub.Core.Updates;

namespace TrimuiSmartHub.Core.Tests;

public class PlatformRegistryTests
{
    [Fact]
    public void Loads_bundled_platforms()
    {
        var registry = PlatformRegistry.LoadDefault();

        Assert.True(registry.All.Count >= 25);
        Assert.Equal("Game Boy Advance", registry.Find("gba")!.Name);
        Assert.True(registry.Find("NEOGEO")!.Arcade);
        Assert.All(registry.All, p => Assert.False(string.IsNullOrEmpty(p.Name)));
    }

    [Theory]
    [InlineData("gba", "GBA")]
    [InlineData(".SFC", "SFC")]
    [InlineData("z64", "N64")]
    [InlineData("cso", "PPSSPP")]
    public void Unambiguous_extensions_map_to_platform(string extension, string platformId)
    {
        Assert.Equal(platformId, PlatformRegistry.LoadDefault().FindByExtension(extension)?.Id);
    }

    [Theory]
    [InlineData("zip")]
    [InlineData("iso")]
    [InlineData("cue")]
    [InlineData("chd")]
    public void Ambiguous_extensions_map_to_nothing(string extension)
    {
        Assert.Null(PlatformRegistry.LoadDefault().FindByExtension(extension));
    }

    [Fact]
    public void Unknown_folder_becomes_generic_platform()
    {
        var platform = PlatformRegistry.LoadDefault().GetOrUnknown("PICO8");
        Assert.True(platform.IsUnknown);
        Assert.Equal("PICO8", platform.Id);
    }
}

public class RelPathTests
{
    [Theory]
    [InlineData("Super Mario Bros. 3.nes", "Super Mario Bros. 3")]
    [InlineData("Roms/GBA/Pokemon - Emerald Version (USA, Europe).gba", "Pokemon - Emerald Version (USA, Europe)")]
    [InlineData("noext", "noext")]
    public void Base_name_keeps_dots_in_titles(string path, string expected)
    {
        Assert.Equal(expected, RelPath.GetBaseName(path));
    }

    [Theory]
    [InlineData("Emus/GBA/../../Roms/GBA", "Roms/GBA")]
    [InlineData("Emus\\GBA\\.\\x", "Emus/GBA/x")]
    [InlineData("../outside", null)]
    [InlineData("a/../../b", null)]
    public void Resolve_handles_relative_segments(string path, string? expected)
    {
        Assert.Equal(expected, RelPath.Resolve(path));
    }

    [Fact]
    public void Sanitize_removes_invalid_characters()
    {
        Assert.Equal("Zelda_ A Link_", RelPath.SanitizeFileName("Zelda: A Link?"));
    }
}

public class TitleMatcherTests
{
    private static readonly string[] Catalog =
    {
        "Super Mario World (USA)",
        "Super Mario World (Europe)",
        "Super Mario World 2 - Yoshi's Island (USA, Europe)",
        "Super Mario Kart (USA)",
        "Mario Paint (USA)",
        "Legend of Zelda, The - A Link to the Past (USA)",
    };

    [Fact]
    public void Prefers_exact_base_title_and_query_region()
    {
        var best = TitleMatcher.FindBest("Super Mario World (Europe)", Catalog)[0];
        Assert.Equal("Super Mario World (Europe)", best.Candidate);
        Assert.True(best.Score >= TitleMatcher.AutoAcceptScore);
    }

    [Fact]
    public void Prefers_usa_when_query_has_no_region()
    {
        Assert.Equal("Super Mario World (USA)", TitleMatcher.FindBest("super mario world", Catalog)[0].Candidate);
    }

    [Fact]
    public void Short_query_does_not_auto_accept_a_longer_title()
    {
        // The old matcher returned a perfect score for any title *containing* the query.
        var matches = TitleMatcher.FindBest("Mario", Catalog);
        Assert.All(matches, m => Assert.True(m.Score < TitleMatcher.AutoAcceptScore));
    }

    [Fact]
    public void Abbreviated_name_still_gets_suggestions_for_review()
    {
        var matches = TitleMatcher.FindBest("zelda lttp", Catalog);
        Assert.Equal("Legend of Zelda, The - A Link to the Past (USA)", matches[0].Candidate);
        Assert.True(matches[0].Score < TitleMatcher.AutoAcceptScore);
    }

    [Fact]
    public void Handles_reordered_words()
    {
        var best = TitleMatcher.FindBest("The Legend of Zelda - A Link to the Past", Catalog)[0];
        Assert.Equal("Legend of Zelda, The - A Link to the Past (USA)", best.Candidate);
        Assert.True(best.Score >= TitleMatcher.AutoAcceptScore);
    }
}

public class SaveFileMatcherTests
{
    [Theory]
    [InlineData("Game.srm", ".srm")]
    [InlineData("Game.state", ".state")]
    [InlineData("Game.state3", ".state3")]
    [InlineData("Game.state.auto", ".state.auto")]
    [InlineData("Game.state1.png", ".state1.png")]
    [InlineData("game.SAV", ".SAV")]
    [InlineData("Game_0.dss", "_0.dss")]
    [InlineData("Game (USA).srm", null)]
    [InlineData("Game.v2.srm", null)]
    [InlineData("Game.gba", null)]
    [InlineData("Game", null)]
    public void Recognizes_saves_of_a_rom(string fileName, string? expectedSuffix)
    {
        Assert.Equal(expectedSuffix, SaveFileMatcher.GetSuffix(fileName, "Game"));
    }
}

public class LibretroThumbnailsTests
{
    [Fact]
    public void Sanitizes_like_libretro()
    {
        Assert.Equal("Ys I _ II_ The _Best_", LibretroThumbnails.SanitizeTitle("Ys I & II: The \"Best\""));
    }

    [Fact]
    public void Builds_escaped_url()
    {
        var url = LibretroThumbnails.BuildUrl("Nintendo - Game Boy", MediaKind.Boxart, "Tetris (World) (Rev 1)");
        Assert.Equal("https://thumbnails.libretro.com/Nintendo%20-%20Game%20Boy/Named_Boxarts/Tetris%20%28World%29%20%28Rev%201%29.png", url.AbsoluteUri);
    }

    [Fact]
    public void Parses_directory_listing()
    {
        const string html = """
            <a href="../">../</a>
            <a href="Tetris%20(World).png">Tetris (World).png</a>
            <a href="Bomberman%20'94%20+%20Extra.png">x</a>
            <a href="?C=M;O=A">Last modified</a>
            """;
        Assert.Equal(new[] { "Tetris (World)", "Bomberman '94 + Extra" }, LibretroThumbnails.ParseIndex(html));
    }
}

public class RetrosticParsingTests
{
    [Fact]
    public void Keeps_title_image_and_region_of_the_same_row()
    {
        const string html = """
            <table><tbody>
              <tr itemtype="https://schema.org/VideoGame">
                <td class="d-none d-sm-table-cell"><a href="/roms/gba/game-a" title="Game A (USA) ROM"><img data-src="/img/a.jpg"></a></td>
                <td>no flag here</td>
              </tr>
              <tr itemtype="https://schema.org/VideoGame">
                <td class="d-none d-sm-table-cell"><a href="/roms/gba/game-b" title="Game B Roms"><img data-src="/img/b.jpg"></a></td>
                <td><img src="/flags/eu.png"> Europe</td>
              </tr>
            </tbody></table>
            <a class="page-link" href="/roms/gba?page=2">&gt;</a>
            """;

        var games = new RetrosticClient(new HttpClient()).ParseListing(html, "GBA", out var next);

        Assert.Equal(2, games.Count);
        Assert.Equal("Game A ROM", games[0].Title.Replace("  ", " "));
        Assert.Equal(string.Empty, games[0].Region);
        Assert.Equal("Game B", games[1].Title);
        Assert.Equal("Europe", games[1].Region);
        Assert.Equal("https://www.retrostic.com/roms/gba/game-b", games[1].DownloadPage.AbsoluteUri);
        Assert.Equal("https://www.retrostic.com/roms/gba?page=2", next!.AbsoluteUri);
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.4", "1.4.0")]
    [InlineData("v2.0.0-beta", "2.0.0")]
    public void Parses_release_tags(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseVersion(tag));
    }

    [Fact]
    public void Rejects_invalid_tags()
    {
        Assert.Null(UpdateChecker.ParseVersion("latest"));
    }
}

public class SyncFormatTests
{
    [Fact]
    public void Formats_sizes()
    {
        Assert.Equal($"{1.5:0.#} GB", SyncPlanner.Format(1610612736));
        Assert.Equal("512 B", SyncPlanner.Format(512));
    }
}
