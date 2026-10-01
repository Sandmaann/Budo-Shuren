using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class FormularSchutzTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly FormularSchutz _schutz;

    public FormularSchutzTests()
    {
        _schutz = new FormularSchutz(new EphemeralDataProtectionProvider(), _zeit);
    }

    [Fact]
    public void Menschliches_Ausfuellen_ist_unverdaechtig()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(20));

        _schutz.IstVerdaechtig(honeypot: null, zeitstempel).ShouldBeFalse();
        _schutz.IstVerdaechtig(honeypot: "", zeitstempel).ShouldBeFalse();
    }

    [Fact]
    public void Ausgefuellter_Honeypot_ist_verdaechtig()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(20));

        _schutz.IstVerdaechtig("https://spam.example", zeitstempel).ShouldBeTrue();
    }

    [Fact]
    public void Zu_schnelles_Absenden_ist_verdaechtig()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(1));

        _schutz.IstVerdaechtig(null, zeitstempel).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("638000000000000000")] // unverschlüsselt, also selbst gebaut
    [InlineData("kaputt")]
    public void Fehlender_oder_manipulierter_Zeitstempel_ist_verdaechtig(string? zeitstempel)
    {
        _schutz.IstVerdaechtig(null, zeitstempel).ShouldBeTrue();
    }
}
