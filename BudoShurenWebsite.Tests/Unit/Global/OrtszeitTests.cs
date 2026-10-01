using BudoShurenWebsite.Global;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Global;

[Trait("Category", "Unit")]
public class OrtszeitTests
{
    [Fact]
    public void Sommerzeit_ist_UTC_plus_2()
    {
        var zeit = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        Ortszeit.Jetzt(zeit).ShouldBe(new DateTime(2026, 7, 1, 12, 0, 0));
    }

    [Fact]
    public void Winterzeit_ist_UTC_plus_1()
    {
        Ortszeit.AusUtc(new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc)).ShouldBe(new DateTime(2026, 12, 1, 11, 0, 0));
    }

    [Fact]
    public void Hin_und_zurueck_ergibt_denselben_Zeitpunkt()
    {
        var utc = new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);

        Ortszeit.NachUtc(Ortszeit.AusUtc(utc)).ShouldBe(utc);
    }

    [Fact]
    public void Doppelte_Stunde_im_Herbst_wird_als_Winterzeit_gelesen()
    {
        // 25.10.2026: 03:00 Sommerzeit -> 02:00 Winterzeit, 02:30 gibt es zweimal
        Ortszeit.NachUtc(new DateTime(2026, 10, 25, 2, 30, 0)).ShouldBe(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Fehlende_Stunde_im_Fruehjahr_wird_nach_hinten_verschoben()
    {
        // 29.03.2026: 02:00 -> 03:00, 02:30 gibt es nicht; gelesen als 03:30 Sommerzeit = 01:30 UTC
        Ortszeit.NachUtc(new DateTime(2026, 3, 29, 2, 30, 0)).ShouldBe(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc));
    }
}
