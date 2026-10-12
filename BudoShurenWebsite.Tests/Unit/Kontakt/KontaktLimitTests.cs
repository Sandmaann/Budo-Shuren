using BudoShurenWebsite.Services.Kontakt;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Kontakt;

[Trait("Category", "Unit")]
public class KontaktLimitTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly KontaktLimit _limit;

    public KontaktLimitTests()
    {
        _limit = new KontaktLimit(_zeit);
    }

    [Fact]
    public void Je_IP_Adresse_nur_die_erlaubte_Anzahl()
    {
        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            _limit.Annehmen("203.0.113.1", $"absender{i}@example.org").ShouldBeTrue();

        _limit.Annehmen("203.0.113.1", "noch-einer@example.org").ShouldBeFalse();
        _limit.Annehmen("203.0.113.2", "noch-einer@example.org").ShouldBeTrue("andere Adresse, anderer Absender");
    }

    [Fact]
    public void Je_E_Mail_Adresse_nur_die_erlaubte_Anzahl_auch_bei_wechselnder_IP()
    {
        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            _limit.Annehmen($"203.0.113.{i}", "max@example.org").ShouldBeTrue();

        _limit.Annehmen("203.0.113.99", " MAX@example.org ").ShouldBeFalse();
    }

    [Fact]
    public void Ohne_IP_Adresse_gilt_die_Grenze_je_E_Mail_Adresse()
    {
        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            _limit.Annehmen(null, "max@example.org").ShouldBeTrue();

        _limit.Annehmen(null, "max@example.org").ShouldBeFalse();
        // Unbekannte IP-Adressen zählen nicht als ein gemeinsamer Absender
        _limit.Annehmen(null, "moritz@example.org").ShouldBeTrue();
    }

    [Fact]
    public void Insgesamt_nur_die_erlaubte_Anzahl()
    {
        for (var i = 0; i < KontaktLimit.Insgesamt; i++)
            _limit.Annehmen($"203.0.113.{i}", $"absender{i}@example.org").ShouldBeTrue();

        _limit.Annehmen("198.51.100.1", "neu@example.org").ShouldBeFalse();
    }

    [Fact]
    public void Nach_dem_Zeitfenster_geht_es_wieder()
    {
        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            _limit.Annehmen("203.0.113.1", "max@example.org").ShouldBeTrue();
        _limit.Annehmen("203.0.113.1", "max@example.org").ShouldBeFalse();

        _zeit.Advance(KontaktLimit.Zeitfenster - TimeSpan.FromSeconds(1));
        _limit.Annehmen("203.0.113.1", "max@example.org").ShouldBeFalse();

        _zeit.Advance(TimeSpan.FromSeconds(1));
        _limit.Annehmen("203.0.113.1", "max@example.org").ShouldBeTrue();
    }

    [Fact]
    public void Abgelehnte_Nachrichten_zaehlen_nicht_mit()
    {
        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            _limit.Annehmen("203.0.113.1", "max@example.org");
        for (var i = 0; i < 100; i++)
            _limit.Annehmen("203.0.113.1", "max@example.org").ShouldBeFalse();

        // Die Gesamtgrenze ist dadurch nicht erreicht
        _limit.Annehmen("203.0.113.2", "moritz@example.org").ShouldBeTrue();
    }
}
