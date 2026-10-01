using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeranstaltungRechteTests
{
    private static VerwaltungsBenutzer Admin => new("1", "Admin", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);

    private static VerwaltungsBenutzer Leiter(string? abteilung) => new("2", "Leiter", IstAdmin: false, IstAbteilungsleiter: true, abteilung);

    private static VerwaltungsBenutzer Editor => new("3", "Editor", IstAdmin: false, IstAbteilungsleiter: false, Abteilung: "Aikido");

    [Fact]
    public void Admin_darf_alles()
    {
        VeranstaltungRechte.DarfVerwalten(Admin, "Aikido", "Aikido").ShouldBeTrue();
        VeranstaltungRechte.DarfVerwalten(Admin, null, null).ShouldBeTrue();
    }

    [Theory]
    [InlineData("aikido-id", "Aikido", "aikido-id", true)]  // Konto speichert die Id
    [InlineData("aikido-id", "Aikido", "Aikido", true)]     // Konto speichert den Namen (Registrierung)
    [InlineData("aikido-id", "Aikido", "AIKIDO", true)]     // Groß-/Kleinschreibung egal
    [InlineData("bujinkan", "Bujinkan", "Aikido", false)]   // fremde Abteilung
    [InlineData(null, null, "Aikido", false)]               // Gesamtverein nur für Admins
    [InlineData("aikido-id", "Aikido", null, false)]        // Konto ohne Abteilung
    public void Abteilungsleiter_nur_eigene_Abteilung(string? abteilungId, string? abteilungName, string? kontoAbteilung, bool erwartet)
    {
        VeranstaltungRechte.DarfVerwalten(Leiter(kontoAbteilung), abteilungId, abteilungName).ShouldBe(erwartet);
    }

    [Fact]
    public void Editoren_und_Mitglieder_duerfen_das_Modul_nicht_nutzen()
    {
        VeranstaltungRechte.DarfModulNutzen(Editor).ShouldBeFalse();
        VeranstaltungRechte.DarfVerwalten(Editor, "Aikido", "Aikido").ShouldBeFalse();
    }

    [Fact]
    public void VerwaltungsBenutzer_aus_UserService()
    {
        var benutzer = new UserWithRoles
        {
            User = new ApplicationUser { Id = "u1", UserName = "max", Vorname = "Max", Name = "Muster", Abteilung = "Aikido" },
            Roles = [Roles.Abteilungsleiter]
        };

        VerwaltungsBenutzer.Aus(benutzer).ShouldBe(new VerwaltungsBenutzer("u1", "Max Muster", false, true, "Aikido"));
        VerwaltungsBenutzer.Aus(new UserWithRoles()).ShouldBeNull();
        VerwaltungsBenutzer.Aus(null).ShouldBeNull();
    }
}
