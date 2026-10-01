using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Mail;

[Trait("Category", "Unit")]
public class EmailWarteschlangeTests : IDisposable
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    // Hinzufuegen speichert nicht selbst, deshalb genügt ein Kontext ohne Datenbankverbindung
    private readonly ApplicationDbContext _kontext = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=nicht-verwendet;Database=Warteschlange")
        .Options);

    private readonly EmailWarteschlange _warteschlange = new(new FakeTimeProvider(Jetzt), new EmailVersandSignal());

    public void Dispose() => _kontext.Dispose();

    private EmailAusgang HinzugefuegterEintrag() =>
        _kontext.ChangeTracker.Entries<EmailAusgang>().ShouldHaveSingleItem().Entity;

    [Fact]
    public void Hinzufuegen_legt_wartenden_sofort_faelligen_Eintrag_an_ohne_zu_speichern()
    {
        _warteschlange.Hinzufuegen(_kontext, new AusgehendeEmail(" teilnehmer@example.org ", "Betreff", "<p>Text</p>")
        {
            AntwortAn = "kontakt@example.org",
            Prioritaet = EmailPrioritaet.Hoch,
            BezugTyp = "Anmeldung",
            BezugId = 7
        });

        _kontext.ChangeTracker.Entries<EmailAusgang>().ShouldHaveSingleItem().State.ShouldBe(EntityState.Added);
        var eintrag = HinzugefuegterEintrag();
        eintrag.An.ShouldBe("teilnehmer@example.org");
        eintrag.AntwortAn.ShouldBe("kontakt@example.org");
        eintrag.Betreff.ShouldBe("Betreff");
        eintrag.Html.ShouldBe("<p>Text</p>");
        eintrag.Prioritaet.ShouldBe(EmailPrioritaet.Hoch);
        eintrag.Status.ShouldBe(EmailStatus.Wartend);
        eintrag.Versuche.ShouldBe(0);
        eintrag.ErstelltUtc.ShouldBe(Jetzt.UtcDateTime);
        eintrag.FaelligAbUtc.ShouldBe(Jetzt.UtcDateTime);
        eintrag.BezugTyp.ShouldBe("Anmeldung");
        eintrag.BezugId.ShouldBe(7);
        eintrag.AnhaengeJson.ShouldBeNull();
    }

    [Fact]
    public void Standardprioritaet_ist_normal()
    {
        _warteschlange.Hinzufuegen(_kontext, new AusgehendeEmail("a@example.org", "Betreff", "x"));

        HinzugefuegterEintrag().Prioritaet.ShouldBe(EmailPrioritaet.Normal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("keine-adresse")]
    [InlineData("Max Muster <max@example.org>")]
    [InlineData("a@example.org, b@example.org")]
    public void Ungueltige_Empfaengeradresse_wird_sofort_abgelehnt(string adresse)
    {
        Should.Throw<ArgumentException>(() =>
            _warteschlange.Hinzufuegen(_kontext, new AusgehendeEmail(adresse, "Betreff", "x")));

        _kontext.ChangeTracker.Entries<EmailAusgang>().ShouldBeEmpty();
    }

    [Fact]
    public void Ungueltige_Antwortadresse_wird_abgelehnt()
    {
        Should.Throw<ArgumentException>(() =>
            _warteschlange.Hinzufuegen(_kontext, new AusgehendeEmail("a@example.org", "Betreff", "x") { AntwortAn = "kaputt" }));
    }

    [Fact]
    public void Leerer_Betreff_wird_abgelehnt()
    {
        Should.Throw<ArgumentException>(() =>
            _warteschlange.Hinzufuegen(_kontext, new AusgehendeEmail("a@example.org", " ", "x")));
    }
}
