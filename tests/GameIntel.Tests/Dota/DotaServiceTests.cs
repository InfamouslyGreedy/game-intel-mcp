using System.Text.Json;
using GameIntel.Core;
using GameIntel.Providers.Dota;
using GameIntel.Providers.Dota.Models;

namespace GameIntel.Tests.Dota;

public sealed class DotaServiceTests
{
    // A trimmed copy of OpenDota's /heroes response, including the tricky names.
    private static readonly IReadOnlyList<Hero> Heroes = JsonSerializer.Deserialize<List<Hero>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "opendota-heroes.json")),
        OpenDotaClient.Json)!;

    [Theory]
    [InlineData("am", "Anti-Mage")]
    [InlineData("antimage", "Anti-Mage")]
    [InlineData("anti mage", "Anti-Mage")]
    [InlineData("pa", "Phantom Assassin")]
    [InlineData("sf", "Shadow Fiend")]
    [InlineData("nevermore", "Shadow Fiend")]
    [InlineData("np", "Nature's Prophet")]
    [InlineData("natures prophet", "Nature's Prophet")]
    [InlineData("furion", "Nature's Prophet")]
    [InlineData("jugg", "Juggernaut")]
    [InlineData("pango", "Pangolier")]
    public void Resolves_hero_names_players_actually_type(string query, string expected)
    {
        Assert.Equal(expected, DotaService.ResolveHero(Heroes, query).LocalizedName);
    }

    [Fact]
    public void Unknown_hero_lists_suggestions()
    {
        var ex = Assert.Throws<NotFoundException>(() => DotaService.ResolveHero(Heroes, "jugernaut"));
        Assert.Contains("Juggernaut", ex.Suggestions);
    }

    [Fact]
    public void Fixture_deserializes_snake_case_fields()
    {
        var am = Heroes.Single(h => h.Id == 1);
        Assert.Equal("npc_dota_hero_antimage", am.Name);
        Assert.Equal("agi", am.PrimaryAttr);
        Assert.Contains("Carry", am.Roles!);
    }
}
