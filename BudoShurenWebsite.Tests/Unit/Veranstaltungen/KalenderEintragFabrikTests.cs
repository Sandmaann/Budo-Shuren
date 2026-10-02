using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class KalenderEintragFabrikTests
{
    private static readonly DateTime Jetzt = new(2026, 10, 1, 12, 0, 0);

    private static Veranstaltung Veranstaltung(
        VeranstaltungStatus status = VeranstaltungStatus.Veroeffentlicht,
        VeranstaltungSichtbarkeit sichtbarkeit = VeranstaltungSichtbarkeit.Oeffentlich) => new()
    {
        Titel = "Herbstseminar",
        Slug = "herbstseminar",
        Ort = "Dojo",
        Status = status,
        Sichtbarkeit = sichtbarkeit
    };

    private static VeranstaltungsTag Tag(bool abgesagt = false, string? titel = null) => new()
    {
        Id = 7,
        Datum = new DateOnly(2026, 11, 14),
        Beginn = new TimeOnly(10, 0),
        Ende = new TimeOnly(16, 30),
        Titel = titel,
        Abgesagt = abgesagt
    };

    [Theory]
    [InlineData(VeranstaltungStatus.Entwurf, false)]
    [InlineData(VeranstaltungStatus.Veroeffentlicht, true)]
    [InlineData(VeranstaltungStatus.Abgesagt, false)]
    [InlineData(VeranstaltungStatus.Abgeschlossen, true)] // bleibt als Rückblick stehen
    [InlineData(VeranstaltungStatus.Archiviert, true)]
    public void Nur_veroeffentlichte_und_vergangene_Veranstaltungen_stehen_im_Kalender(VeranstaltungStatus status, bool erwartet)
    {
        KalenderEintragFabrik.GehoertInDenKalender(Veranstaltung(status), Tag()).ShouldBe(erwartet);
    }

    [Fact]
    public void Nur_per_Link_sichtbare_Veranstaltungen_und_abgesagte_Tage_nicht()
    {
        KalenderEintragFabrik.GehoertInDenKalender(Veranstaltung(sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink), Tag()).ShouldBeFalse();
        KalenderEintragFabrik.GehoertInDenKalender(Veranstaltung(), Tag(abgesagt: true)).ShouldBeFalse();
    }

    [Fact]
    public void Neuer_Eintrag_uebernimmt_Termin_Ort_Link_und_Verknuepfung()
    {
        var eintrag = new AppointmentData();

        KalenderEintragFabrik.Uebernehmen(eintrag, Veranstaltung(), Tag(titel: "Tag 1"), Jetzt);

        eintrag.VeranstaltungsTagId.ShouldBe(7);
        eintrag.IsReadonly.ShouldBeTrue();
        eintrag.Subject.ShouldBe("Herbstseminar – Tag 1");
        eintrag.Location.ShouldBe("Dojo");
        eintrag.StartTime.ShouldBe(new DateTime(2026, 11, 14, 10, 0, 0));
        eintrag.EndTime.ShouldBe(new DateTime(2026, 11, 14, 16, 30, 0));
        eintrag.Description.ShouldContain("/veranstaltungen/herbstseminar");
        eintrag.Abteilung.ShouldBe(KalenderEintragFabrik.AbteilungGesamtverein);
        eintrag.ShowInMonth.ShouldBeTrue();
        eintrag.Created.ShouldBe(Jetzt);
        eintrag.EntryCreatedBy.ShouldBe(KalenderEintragFabrik.Ersteller);
    }

    [Fact]
    public void Offenes_Ende_wird_eine_halbe_Stunde_ohne_angezeigte_Endzeit()
    {
        var tag = Tag(titel: "Essen");
        tag.Beginn = new TimeOnly(19, 0);
        tag.Ende = null;
        var eintrag = new AppointmentData();

        KalenderEintragFabrik.Uebernehmen(eintrag, Veranstaltung(), tag, Jetzt);

        eintrag.StartTime.ShouldBe(new DateTime(2026, 11, 14, 19, 0, 0));
        // Kalender.razor zeigt die Uhrzeit erst ab mehr als 30 Minuten; so erscheint keine erfundene Endzeit
        eintrag.EndTime.ShouldBe(new DateTime(2026, 11, 14, 19, 30, 0));
    }

    [Fact]
    public void Abteilung_der_Veranstaltung_bestimmt_die_Kalenderfarbe()
    {
        var veranstaltung = Veranstaltung();
        veranstaltung.Abteilung = new Abteilung { ID = "Aikido", Name = "Aikido" };
        var eintrag = new AppointmentData();

        KalenderEintragFabrik.Uebernehmen(eintrag, veranstaltung, Tag(), Jetzt);

        eintrag.Abteilung.ShouldBe("Aikido");
        eintrag.Subject.ShouldBe("Herbstseminar");
    }

    [Fact]
    public void Bestehender_Eintrag_behaelt_Erstellungsdaten()
    {
        var erstellt = new DateTime(2026, 9, 1);
        var eintrag = new AppointmentData { Id = 3, Created = erstellt, EntryCreatedBy = KalenderEintragFabrik.Ersteller };

        KalenderEintragFabrik.Uebernehmen(eintrag, Veranstaltung(), Tag(), Jetzt);

        eintrag.Created.ShouldBe(erstellt);
        eintrag.LastChange.ShouldBe(Jetzt);
    }
}
