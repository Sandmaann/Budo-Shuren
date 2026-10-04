using BudoShurenWebsite.Services.Entwuerfe;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace BudoShurenWebsite.Tests.Unit;

[Trait("Category", "Unit")]
public class EntwurfSicherungTests
{
    private sealed class Formular
    {
        public string Titel { get; set; } = string.Empty;

        public List<int> BildIds { get; set; } = [];
    }

    /// <summary>Hält den einen Entwurf im Speicher und zählt die Zugriffe.</summary>
    private sealed class FakeSpeicher : IEntwurfSpeicher
    {
        public string? Daten { get; set; }

        public IReadOnlyCollection<int> BildIds { get; private set; } = [];

        public int Sicherungen { get; private set; }

        public bool LoeschenScheitert { get; set; }

        public Task<GeladenerEntwurf<T>?> LadenAsync<T>(string benutzerId, string schluessel, CancellationToken abbruch = default) where T : class =>
            Task.FromResult(Daten is null ? null : new GeladenerEntwurf<T>(JsonSerializer.Deserialize<T>(Daten)!, DateTime.UtcNow));

        public Task SpeichernAsync(string benutzerId, string schluessel, string daten, IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            (benutzerId, schluessel).ShouldBe(("olga", "seite:1"));
            Daten = daten;
            BildIds = bildIds;
            Sicherungen++;
            return Task.CompletedTask;
        }

        public Task LoeschenAsync(string benutzerId, string schluessel, CancellationToken abbruch = default)
        {
            if (LoeschenScheitert)
                throw new InvalidOperationException("Datenbank nicht erreichbar");
            Daten = null;
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<int>> FehlendeBilderAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }

    private readonly FakeSpeicher _speicher = new();
    private Formular? _formular = new() { Titel = "Geladen" };

    private EntwurfSicherung<Formular> Sicherung() =>
        new(_speicher, NullLogger.Instance, "olga", "seite:1", () => _formular, f => f.BildIds);

    [Fact]
    public async Task Ohne_Aenderung_entsteht_kein_Entwurf()
    {
        await using var sicherung = Sicherung();
        sicherung.AusgangMerken();

        await sicherung.SichernAsync();

        _speicher.Sicherungen.ShouldBe(0);
    }

    [Fact]
    public async Task Jede_Aenderung_wird_einmal_gesichert_mit_ihren_Bildern()
    {
        await using var sicherung = Sicherung();
        sicherung.AusgangMerken();

        _formular!.Titel = "Geändert";
        _formular.BildIds.AddRange([5, 5, 9]);
        await sicherung.SichernAsync();
        await sicherung.SichernAsync();

        _speicher.Sicherungen.ShouldBe(1);
        _speicher.Daten.ShouldBe(EntwurfSpeicher.AlsJson(_formular));
        _speicher.BildIds.ShouldBe([5, 9]);

        _formular.Titel = "Noch einmal geändert";
        await sicherung.SichernAsync();
        _speicher.Sicherungen.ShouldBe(2);
    }

    [Fact]
    public async Task Solange_nichts_geladen_ist_wird_nichts_gesichert()
    {
        _formular = null;
        await using var sicherung = Sicherung();
        sicherung.AusgangMerken();

        await sicherung.SichernAsync();

        _speicher.Sicherungen.ShouldBe(0);
    }

    [Fact]
    public async Task Nach_dem_Speichern_entsteht_der_Entwurf_nicht_gleich_wieder()
    {
        await using var sicherung = Sicherung();
        sicherung.AusgangMerken();
        _formular!.Titel = "Geändert";
        await sicherung.SichernAsync();

        // Seite hat gespeichert: Entwurf weg, die Seite lädt gleich neu
        await sicherung.LoeschenAsync();
        _speicher.Daten.ShouldBeNull();
        await sicherung.SichernAsync();
        _speicher.Daten.ShouldBeNull("vor dem Neuladen wird nicht gesichert");

        _formular = new Formular { Titel = "Geändert", BildIds = { 1 } };
        sicherung.AusgangMerken();
        await sicherung.SichernAsync();
        _speicher.Daten.ShouldBeNull("der neu geladene Stand ist der Ausgang");

        _formular.Titel = "Weiter bearbeitet";
        await sicherung.SichernAsync();
        _speicher.Daten.ShouldNotBeNull();
    }

    [Fact]
    public async Task Wiederhergestellter_Entwurf_ist_der_Ausgang_und_wird_nicht_erneut_gesichert()
    {
        _speicher.Daten = EntwurfSpeicher.AlsJson(new Formular { Titel = "Aus dem Entwurf" });
        await using var sicherung = Sicherung();

        var entwurf = (await sicherung.LadenAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        // Während die Seite lädt, steht noch der Stand aus der Datenbank im Formular
        await sicherung.SichernAsync();
        _formular = entwurf.Daten;
        sicherung.AusgangMerken();
        await sicherung.SichernAsync();

        _speicher.Sicherungen.ShouldBe(0);
        _formular.Titel.ShouldBe("Aus dem Entwurf");
    }

    [Fact]
    public async Task Scheitert_das_Loeschen_laeuft_die_Seite_weiter()
    {
        _speicher.LoeschenScheitert = true;
        await using var sicherung = Sicherung();

        await Should.NotThrowAsync(sicherung.LoeschenAsync);
    }

    [Fact]
    public async Task Gestartet_sichert_sie_Aenderungen_von_selbst()
    {
        await using var sicherung = Sicherung();
        sicherung.AusgangMerken();
        var aufrufeImKreislauf = 0;
        sicherung.Starten(async arbeit =>
        {
            aufrufeImKreislauf++;
            await arbeit();
        });

        _formular!.Titel = "Geändert";
        var ende = DateTime.UtcNow.AddSeconds(15);
        while (_speicher.Sicherungen == 0 && DateTime.UtcNow < ende)
            await Task.Delay(100, TestContext.Current.CancellationToken);

        _speicher.Sicherungen.ShouldBe(1);
        aufrufeImKreislauf.ShouldBeGreaterThan(0, "der Stand wird nur über InvokeAsync der Komponente gelesen");
    }
}
