using BudoShurenWebsite.Data;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class AnmeldeVorbelegungTests
{
    private static ApplicationUser Benutzer(string vorname = "Max", string name = "Muster", string? email = "max@example.org") =>
        new() { Vorname = vorname, Name = name, Email = email };

    [Fact]
    public void Leeres_Formular_bekommt_Name_und_Email_aus_dem_Konto()
    {
        var eingabe = new AnmeldeEingabe();

        AnmeldeVorbelegung.Uebernehmen(eingabe, Benutzer());

        eingabe.Vorname.ShouldBe("Max");
        eingabe.Nachname.ShouldBe("Muster");
        eingabe.Email.ShouldBe("max@example.org");
        eingabe.Telefon.ShouldBeNull();
    }

    [Fact]
    public void Ausgefuellte_Felder_bleiben_unveraendert()
    {
        var eingabe = new AnmeldeEingabe { Vorname = "Erika", Nachname = "Beispiel", Email = "erika@example.org" };

        AnmeldeVorbelegung.Uebernehmen(eingabe, Benutzer());

        eingabe.Vorname.ShouldBe("Erika");
        eingabe.Nachname.ShouldBe("Beispiel");
        eingabe.Email.ShouldBe("erika@example.org");
    }

    [Fact]
    public void Im_Konto_fehlende_Angaben_bleiben_leer()
    {
        var eingabe = new AnmeldeEingabe();

        AnmeldeVorbelegung.Uebernehmen(eingabe, Benutzer(vorname: " ", name: "", email: null));

        eingabe.Vorname.ShouldBeNull();
        eingabe.Nachname.ShouldBeNull();
        eingabe.Email.ShouldBeNull();
    }
}
