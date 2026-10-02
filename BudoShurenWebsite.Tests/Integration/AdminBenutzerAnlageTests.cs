using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class AdminBenutzerAnlageTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Email = "admin@example.org";
    private const string GueltigesPasswort = "Sicher-123!";

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));
    private TestWebAppFactory? _app;
    private IServiceScope? _scope;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        // Die App wird für UserManager und die Rollen (Seeding beim Start) gebraucht.
        // "AdminStart" ist in der Testumgebung nicht gesetzt, beim Start entsteht also kein Admin.
        _app = new TestWebAppFactory(Datenbank.Verbindung);
        _scope = _app.Services.CreateScope();
    }

    public override async ValueTask DisposeAsync()
    {
        _scope?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
        await base.DisposeAsync();
    }

    private UserManager<ApplicationUser> UserManager => _scope!.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    private Task<AdminAnlageErgebnis> AusfuehrenAsync(string? email = Email, string? passwort = GueltigesPasswort) =>
        AdminBenutzerAnlage.SicherstellenAsync(UserManager, new AdminStartOptionen { Email = email, Passwort = passwort }, _zeit);

    [DatenbankFact]
    public async Task Legt_freigeschalteten_Admin_an_wenn_keiner_existiert()
    {
        (await UserManager.GetUsersInRoleAsync(Roles.Admin)).ShouldBeEmpty();

        var ergebnis = await AusfuehrenAsync();

        ergebnis.Status.ShouldBe(AdminAnlageStatus.Angelegt);
        var admin = (await UserManager.FindByEmailAsync(Email)).ShouldNotBeNull();
        admin.UserName.ShouldBe(Email);
        admin.EmailConfirmed.ShouldBeTrue();
        admin.Verified.ShouldBeTrue();
        admin.VerifiedBy.ShouldBe("System");
        (await UserManager.IsInRoleAsync(admin, Roles.Admin)).ShouldBeTrue();
        (await UserManager.CheckPasswordAsync(admin, GueltigesPasswort)).ShouldBeTrue();
    }

    [DatenbankFact]
    public async Task Zweiter_Aufruf_aendert_nichts()
    {
        (await AusfuehrenAsync()).Status.ShouldBe(AdminAnlageStatus.Angelegt);

        var ergebnis = await AusfuehrenAsync(email: "anderer@example.org");

        ergebnis.Status.ShouldBe(AdminAnlageStatus.AdminVorhanden);
        (await UserManager.FindByEmailAsync("anderer@example.org")).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Ohne_Konfiguration_wird_nichts_angelegt()
    {
        var ergebnis = await AusfuehrenAsync(email: null, passwort: "");

        ergebnis.Status.ShouldBe(AdminAnlageStatus.NichtKonfiguriert);
        UserManager.Users.ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Zu_schwaches_Passwort_meldet_Fehler_und_legt_keinen_Benutzer_an()
    {
        var ergebnis = await AusfuehrenAsync(passwort: "kurz");

        ergebnis.Status.ShouldBe(AdminAnlageStatus.Fehler);
        ergebnis.Meldung.ShouldNotContain("kurz");
        UserManager.Users.ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Vorhandener_Benutzer_mit_der_Adresse_wird_Admin_und_behaelt_sein_Passwort()
    {
        var gast = new ApplicationUser { UserName = Email, Email = Email, Vorname = "Erika" };
        (await UserManager.CreateAsync(gast, "Eigenes-456!")).Succeeded.ShouldBeTrue();
        (await UserManager.AddToRoleAsync(gast, Roles.Gast)).Succeeded.ShouldBeTrue();

        var ergebnis = await AusfuehrenAsync();

        ergebnis.Status.ShouldBe(AdminAnlageStatus.Befoerdert);
        var admin = (await UserManager.FindByEmailAsync(Email)).ShouldNotBeNull();
        admin.Verified.ShouldBeTrue();
        admin.EmailConfirmed.ShouldBeTrue();
        (await UserManager.GetRolesAsync(admin)).ShouldBe([Roles.Admin]);
        (await UserManager.CheckPasswordAsync(admin, "Eigenes-456!")).ShouldBeTrue();
        UserManager.Users.Count().ShouldBe(1);
    }
}
