using BudoShurenWebsite.Global;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace BudoShurenWebsite.Tests.Unit.Global;

[Trait("Category", "Unit")]
public class BetterStackProtokollTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("token", "")]
    [InlineData(" ", "https://example.invalid")]
    public void Einrichten_ohne_Token_oder_Endpoint_haengt_kein_Ziel_an(string? token, string? endpoint)
    {
        using var fabrik = NeueFabrik();

        var eingerichtet = BetterStackProtokoll.Einrichten(fabrik, Konfiguration(token, endpoint));

        eingerichtet.ShouldBeFalse();
        fabrik.Configuration!.AllTargets.Select(z => z.Name).ShouldBe(["speicher"]);
    }

    [Fact]
    public void Einrichten_mit_Token_und_Endpoint_haengt_das_Ziel_mit_einer_Regel_ab_Info_an()
    {
        using var fabrik = NeueFabrik();

        var eingerichtet = BetterStackProtokoll.Einrichten(fabrik, Konfiguration("token", "https://example.invalid"));

        eingerichtet.ShouldBeTrue();
        var ziel = fabrik.Configuration!.FindTargetByName(BetterStackProtokoll.ZielName);
        ziel.ShouldNotBeNull();
        var regel = fabrik.Configuration.LoggingRules.Last();
        regel.Targets.ShouldBe([ziel]);
        regel.IsLoggingEnabledForLevel(NLog.LogLevel.Info).ShouldBeTrue();
        regel.IsLoggingEnabledForLevel(NLog.LogLevel.Debug).ShouldBeFalse();
    }

    [Fact]
    public void Nach_dem_Neuladen_der_Konfiguration_ist_das_Ziel_wieder_da_und_nur_einmal()
    {
        using var fabrik = NeueFabrik();
        BetterStackProtokoll.Einrichten(fabrik, Konfiguration("token", "https://example.invalid"));

        fabrik.Configuration = NeueKonfiguration(fabrik);

        fabrik.Configuration.AllTargets.Count(z => z.Name == BetterStackProtokoll.ZielName).ShouldBe(1);
        fabrik.Configuration.LoggingRules.Count.ShouldBe(2);
    }

    private static LogFactory NeueFabrik()
    {
        var fabrik = new LogFactory();
        fabrik.Configuration = NeueKonfiguration(fabrik);
        return fabrik;
    }

    private static LoggingConfiguration NeueKonfiguration(LogFactory fabrik)
    {
        var konfiguration = new LoggingConfiguration(fabrik);
        konfiguration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, new MemoryTarget("speicher"), "*");
        return konfiguration;
    }

    private static IConfiguration Konfiguration(string? token, string? endpoint) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BetterStack:SourceToken"] = token,
                ["BetterStack:Endpoint"] = endpoint,
            })
            .Build();
}
