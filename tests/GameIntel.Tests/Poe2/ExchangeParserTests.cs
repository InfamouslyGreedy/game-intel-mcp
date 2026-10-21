using System.Text.Json;
using GameIntel.Providers.Poe2.Analysis;

namespace GameIntel.Tests.Poe2;

public sealed class ExchangeParserTests
{
    internal static JsonElement Fixture(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))).RootElement.Clone();

    [Fact]
    public void Parses_documented_exchange_shape()
    {
        var snapshot = ExchangeParser.Parse(Fixture("poeninja-exchange-currency.json"), "Rise of the Abyssal", "Currency");

        Assert.Equal("Exalted Orb", snapshot.Primary.Name);
        Assert.Equal("Divine Orb", snapshot.Secondary!.Name);
        Assert.Equal(0.0025, snapshot.SecondaryPerPrimary);

        var divine = snapshot.Entries.Single(e => e.Id == "divine");
        Assert.Equal("Divine Orb", divine.Name);
        Assert.Equal(400, divine.Value);
        Assert.Equal(4.2, divine.ChangePercent);
        Assert.Equal("Exalted Orb", divine.MostTradedAgainst);
    }

    [Fact]
    public void Tolerates_numbers_as_strings_missing_sparklines_and_lines_without_ids()
    {
        var snapshot = ExchangeParser.Parse(Fixture("poeninja-exchange-currency.json"), "L", "Currency");

        Assert.Equal(52.5, snapshot.Entries.Single(e => e.Id == "annul").Value);
        Assert.Null(snapshot.Entries.Single(e => e.Id == "greater-jewellers-orb").ChangePercent);
        Assert.Equal(5, snapshot.Entries.Count); // the id-less line is skipped
    }

    [Fact]
    public void Falls_back_to_a_readable_name_for_unknown_ids()
    {
        var snapshot = ExchangeParser.Parse(Fixture("poeninja-exchange-currency.json"), "L", "Currency");
        Assert.Equal("Greater Jewellers Orb", snapshot.Entries.Single(e => e.Id == "greater-jewellers-orb").Name);
    }

    [Fact]
    public void Accepts_item_metadata_as_a_map_and_primary_as_an_object()
    {
        using var doc = JsonDocument.Parse("""
            {
              "core": {
                "items": { "exalted": { "name": "Exalted Orb" }, "divine": { "name": "Divine Orb" } },
                "primary": { "id": "exalted" },
                "rates": { "divine": "0.004" },
                "secondary": "divine"
              },
              "lines": [ { "id": "divine", "primaryValue": 250 } ]
            }
            """);

        var snapshot = ExchangeParser.Parse(doc.RootElement, "L", "Currency");

        Assert.Equal("Exalted Orb", snapshot.Primary.Name);
        Assert.Equal(0.004, snapshot.SecondaryPerPrimary);
        Assert.Equal("Divine Orb", snapshot.Entries[0].Name);
    }

    [Fact]
    public void Garbage_input_yields_an_empty_snapshot_instead_of_throwing()
    {
        using var doc = JsonDocument.Parse("[1,2,3]");
        Assert.Empty(ExchangeParser.Parse(doc.RootElement, "L", "Currency").Entries);
    }

    [Fact]
    public void Parses_leagues_array_in_order()
    {
        using var doc = JsonDocument.Parse("""[{"id":"Rise of the Abyssal","name":"Rise of the Abyssal"},{"id":"Standard","name":"Standard"}]""");

        var leagues = ExchangeParser.ParseLeagues(doc.RootElement);

        Assert.Equal(2, leagues.Count);
        Assert.Equal("Rise of the Abyssal", leagues[0].Id);
    }
}
