using GameIntel.Core;
using GameIntel.Providers.Poe2;
using GameIntel.Providers.Poe2.Analysis;
using GameIntel.Providers.Poe2.Models;

namespace GameIntel.Tests.Poe2;

public sealed class Poe2ServiceTests
{
    private sealed class FakeNinja : IPoeNinjaClient
    {
        public List<string> Requested { get; } = [];

        public Task<IReadOnlyList<League>> GetLeaguesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<League>>([new("Rise of the Abyssal", "Rise of the Abyssal"), new("Standard", "Standard")]);

        public Task<MarketSnapshot> GetExchangeAsync(string league, string category, CancellationToken cancellationToken = default)
        {
            Requested.Add($"{league}/{category}");
            var snapshot = category == "Currency"
                ? ExchangeParser.Parse(ExchangeParserTests.Fixture("poeninja-exchange-currency.json"), league, category)
                : new MarketSnapshot(league, category, new Currency("exalted", "Exalted Orb"), null, null,
                    category == "Runes" ? [new MarketEntry("greater-rune-of-the-storm", "Greater Rune of the Storm", 3, 900, null, null)] : []);
            return Task.FromResult(snapshot);
        }
    }

    private readonly FakeNinja _ninja = new();
    private Poe2Service Service => new(_ninja);

    [Fact]
    public async Task Price_check_defaults_to_current_league_and_converts_to_secondary_currency()
    {
        var price = await Service.PriceCheckAsync("divine", null, null, CancellationToken.None);

        Assert.Equal("Rise of the Abyssal", price.League);
        Assert.Equal("Divine Orb", price.Item);
        Assert.Equal(400, price.Value);
        Assert.Equal("Exalted Orb", price.ValueCurrency);
        Assert.Equal(1, price.ValueInSecondary);
        Assert.Single(_ninja.Requested); // found in the first category searched
    }

    [Fact]
    public async Task Price_check_searches_other_categories_when_not_found_in_currency()
    {
        var price = await Service.PriceCheckAsync("rune of the storm", "standard", null, CancellationToken.None);

        Assert.Equal("Standard", price.League);
        Assert.Equal("Greater Rune of the Storm", price.Item);
        Assert.Equal(new[] { "Standard/Currency", "Standard/Fragments", "Standard/Runes" }, _ninja.Requested);
    }

    [Fact]
    public async Task Unknown_category_is_a_clear_error()
    {
        var ex = await Assert.ThrowsAsync<GameIntelException>(() => Service.MostValuableAsync("Gizmos", null, 5, CancellationToken.None));
        Assert.Contains("SoulCores", ex.Message);
    }

    [Fact]
    public async Task Category_names_are_forgiving()
    {
        var list = await Service.MostValuableAsync("currency", null, 2, CancellationToken.None);

        Assert.Equal(new[] { "Mirror of Kalandra", "Divine Orb" }, list.Items.Select(i => i.Item));
    }

    [Fact]
    public async Task Movers_exclude_illiquid_items()
    {
        var movers = await Service.MoversAsync("Currency", null, 10, minVolume: 10, CancellationToken.None);

        // Annulment moved +80% but on volume 4, so it is filtered; Chaos (-12.5%) leads.
        Assert.Equal("Chaos Orb", movers.Items[0].Item);
        Assert.DoesNotContain(movers.Items, i => i.Item == "Orb of Annulment");
    }

    [Fact]
    public async Task Unknown_league_suggests_valid_ones()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Service.PriceCheckAsync("divine", "Hardcore Necropolis", null, CancellationToken.None));
        Assert.Contains("Standard", ex.Suggestions);
    }
}
