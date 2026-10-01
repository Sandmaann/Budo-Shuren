using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using static BudoShurenWebsite.Models.Enums.AnmeldungStatus;
using static BudoShurenWebsite.Models.Enums.EreignisAkteur;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class AnmeldungStatusUebergaengeTests
{
    [Theory]
    [InlineData(Unbestaetigt, Angemeldet, Teilnehmer)]   // Opt-In bestätigt
    [InlineData(Unbestaetigt, Storniert, Teilnehmer)]
    [InlineData(Unbestaetigt, Abgelehnt, Admin)]
    [InlineData(Angemeldet, Storniert, Teilnehmer)]
    [InlineData(Angemeldet, Storniert, Admin)]
    [InlineData(Angemeldet, Abgelehnt, Admin)]
    [InlineData(Warteliste, Angemeldet, Admin)]
    [InlineData(Warteliste, Angemeldet, EreignisAkteur.System)]
    [InlineData(Storniert, Unbestaetigt, Teilnehmer)]     // erneut angemeldet, mit Opt-In
    [InlineData(Storniert, Angemeldet, Admin)]
    [InlineData(Abgelehnt, Angemeldet, Admin)]            // Ablehnung zurückgenommen
    public void Erlaubte_Wechsel(AnmeldungStatus von, AnmeldungStatus nach, EreignisAkteur akteur)
    {
        AnmeldungStatusUebergaenge.IstErlaubt(von, nach, akteur).ShouldBeTrue();
        Should.NotThrow(() => AnmeldungStatusUebergaenge.Pruefen(von, nach, akteur));
    }

    [Theory]
    [InlineData(Angemeldet, Abgelehnt, Teilnehmer)]       // nur der Admin lehnt ab
    [InlineData(Abgelehnt, Angemeldet, Teilnehmer)]       // Ablehnung nicht selbst aufheben
    [InlineData(Abgelehnt, Unbestaetigt, Teilnehmer)]     // auch nicht über das Formular
    [InlineData(Warteliste, Angemeldet, Teilnehmer)]      // Nachrücken entscheidet die Organisation
    [InlineData(Storniert, Unbestaetigt, Admin)]
    [InlineData(Angemeldet, Unbestaetigt, Teilnehmer)]
    [InlineData(Angemeldet, Angemeldet, Admin)]           // kein Wechsel
    public void Verbotene_Wechsel(AnmeldungStatus von, AnmeldungStatus nach, EreignisAkteur akteur)
    {
        AnmeldungStatusUebergaenge.IstErlaubt(von, nach, akteur).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => AnmeldungStatusUebergaenge.Pruefen(von, nach, akteur));
    }

    [Theory]
    [InlineData(Unbestaetigt, true)]
    [InlineData(Angemeldet, true)]
    [InlineData(Warteliste, true)]
    [InlineData(Storniert, false)]
    [InlineData(Abgelehnt, false)]
    public void Aktive_Anmeldungen(AnmeldungStatus status, bool aktiv)
    {
        AnmeldungStatusUebergaenge.IstAktiv(status).ShouldBe(aktiv);
    }
}
