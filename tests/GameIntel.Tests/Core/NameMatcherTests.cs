using GameIntel.Core.Text;

namespace GameIntel.Tests.Core;

public sealed class NameMatcherTests
{
    private static readonly string[] Heroes = ["Anti-Mage", "Axe", "Shadow Fiend", "Shadow Shaman", "Phantom Assassin", "Juggernaut"];

    [Theory]
    [InlineData("anti mage", "Anti-Mage")]
    [InlineData("ANTI-MAGE", "Anti-Mage")]
    [InlineData("jugg", "Juggernaut")]
    [InlineData("assassin", "Phantom Assassin")]
    [InlineData("pa", "Phantom Assassin")]
    [InlineData("axe", "Axe")]
    public void Finds_forgiving_matches(string query, string expected)
    {
        Assert.Equal(expected, NameMatcher.FindBest(Heroes, query, h => h));
    }

    [Fact]
    public void Exact_match_beats_an_earlier_prefix_match()
    {
        string[] names = ["Divine Orb Shard", "Divine Orb"];
        Assert.Equal("Divine Orb", NameMatcher.FindBest(names, "divine orb", n => n));
    }

    [Fact]
    public void Aliases_are_honoured()
    {
        var match = NameMatcher.FindBest(Heroes, "nevermore", h => h, h => h == "Shadow Fiend" ? ["nevermore"] : []);
        Assert.Equal("Shadow Fiend", match);
    }

    [Fact]
    public void Returns_null_and_suggests_close_names_for_typos()
    {
        Assert.Null(NameMatcher.FindBest(Heroes, "jugernuat", h => h));
        Assert.Contains("Juggernaut", NameMatcher.Suggest(Heroes, "jugernuat", h => h));
    }

    [Theory]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("same", "same", 0)]
    public void Levenshtein_distance_is_correct(string a, string b, int expected)
    {
        Assert.Equal(expected, NameMatcher.Levenshtein(a, b));
    }
}
