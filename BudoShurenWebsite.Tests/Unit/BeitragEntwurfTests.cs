using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Entwuerfe;
using System.Reflection;
using System.Text.Json;

namespace BudoShurenWebsite.Tests.Unit;

/// <summary>Entwürfe der Editoren für Aktuelles und Themen: was im Editor steht, muss den Weg über den Entwurf überstehen.</summary>
[Trait("Category", "Unit")]
public class BeitragEntwurfTests
{
    private static readonly IReadOnlySet<int> KeineFehlenden = new HashSet<int>();

    // Setzt der Editor nicht: Schlüssel, Verweise und wer wann gespeichert hat
    private static readonly string[] NichtImEntwurf = ["Id", "BeitragId", "BlockId", "ErstelltVon", "Erstellt", "GeaendertVon", "Geaendert"];

    private static T UeberJson<T>(T entwurf) => JsonSerializer.Deserialize<T>(EntwurfSpeicher.AlsJson(entwurf))!;

    private static IEnumerable<PropertyInfo> EinfacheEigenschaften(Type typ) =>
        typ.GetProperties().Where(p => p.CanWrite && !NichtImEntwurf.Contains(p.Name)
            && (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) is { } t && (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(DateTime)));

    /// <summary>Gibt jeder einfachen Eigenschaft einen Wert, der nicht der Vorgabe entspricht.</summary>
    private static T Gefuellt<T>(T objekt, int saat) where T : class
    {
        foreach (var p in EinfacheEigenschaften(typeof(T)))
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            object wert = t switch
            {
                _ when t == typeof(string) => $"{p.Name}-{saat}",
                _ when t == typeof(bool) => !(bool)(p.GetValue(objekt) ?? false),
                _ when t == typeof(DateTime) => new DateTime(2026, 1, 1).AddDays(saat),
                _ when t.IsEnum => Enum.GetValues(t).GetValue(1)!,
                _ => Convert.ChangeType(saat + 40, t)
            };
            p.SetValue(objekt, wert);
        }
        return objekt;
    }

    private static void SollGleichSein<T>(T erwartet, T tatsaechlich)
    {
        foreach (var p in EinfacheEigenschaften(typeof(T)))
            p.GetValue(tatsaechlich).ShouldBe(p.GetValue(erwartet), $"{typeof(T).Name}.{p.Name} fehlt im Entwurf");
    }

    [Fact]
    public void Aktuelles_alle_Felder_des_Beitrags_ueberstehen_den_Entwurf()
    {
        var beitrag = Gefuellt(new AktuellesBeitrag(), 1);
        var block = Gefuellt(new AktuellesBlock(), 2);
        block.Bilder.Add(Gefuellt(new AktuellesBild(), 3));
        beitrag.Bloecke.Add(block);

        var wiederhergestellt = new AktuellesBeitrag();
        var entwurf = UeberJson(AktuellesEntwurf.Aus(beitrag, slugManuell: true));
        entwurf.AnwendenAuf(wiederhergestellt, KeineFehlenden);

        entwurf.SlugManuell.ShouldBeTrue();
        SollGleichSein(beitrag, wiederhergestellt);
        var neuerBlock = wiederhergestellt.Bloecke.ShouldHaveSingleItem();
        SollGleichSein(block, neuerBlock);
        SollGleichSein(block.Bilder.Single(), neuerBlock.Bilder.ShouldHaveSingleItem());
    }

    [Fact]
    public void Aktuelles_Entwurf_aendert_vorhandene_Bausteine_entfernt_fehlende_und_legt_neue_an()
    {
        var bleibt = new AktuellesBlock { Id = 10, Typ = AktuellesBlockTyp.BilderGalerie, Bilder = { new AktuellesBild { Id = 100, BildId = 1 }, new AktuellesBild { Id = 101, BildId = 2 } } };
        var entfaellt = new AktuellesBlock { Id = 11, Typ = AktuellesBlockTyp.MarkdownText, MarkdownInhalt = "weg" };
        var beitrag = new AktuellesBeitrag { Id = 5, Titel = "Alt", Bloecke = { bleibt, entfaellt } };

        var entwurf = new AktuellesEntwurf
        {
            Titel = "Neu",
            Bloecke =
            {
                new() { Id = 0, Typ = AktuellesBlockTyp.MarkdownText, Sortierung = 0, MarkdownInhalt = "neuer Text" },
                new() { Id = 10, Typ = AktuellesBlockTyp.BilderGalerie, Sortierung = 1, BilderProReihe = 2,
                    Bilder = { new() { Id = 101, BildId = 2, Sortierung = 0 }, new() { Id = 0, BildId = 7, Sortierung = 1 }, new() { Id = 0, BildId = 8, Sortierung = 2 } } }
            }
        };
        entwurf.BildIds().ShouldBe([2, 7, 8]);

        UeberJson(entwurf).AnwendenAuf(beitrag, new HashSet<int> { 8 });

        beitrag.Titel.ShouldBe("Neu");
        beitrag.Id.ShouldBe(5);
        beitrag.Bloecke.Count.ShouldBe(2);
        beitrag.Bloecke.ShouldNotContain(entfaellt);
        beitrag.Bloecke.Single(b => b.Id == 0).MarkdownInhalt.ShouldBe("neuer Text");
        // Dasselbe Objekt wie vorher: die Datenbank ändert den Baustein, statt ihn zu löschen und neu anzulegen
        beitrag.Bloecke.ShouldContain(bleibt);
        bleibt.BilderProReihe.ShouldBe(2);
        bleibt.Bilder.Select(b => (b.Id, b.BildId, b.BlockId)).ShouldBe([(101, 2, 0), (0, 7, 10)], "Bild 1 entfernt, 7 neu, 8 gibt es nicht mehr");
    }

    [Fact]
    public void Themen_alle_Felder_des_Beitrags_ueberstehen_den_Entwurf()
    {
        var beitrag = Gefuellt(new WissenBeitrag(), 1);
        var block = Gefuellt(new WissenBlock(), 2);
        beitrag.Bloecke.Add(block);

        var wiederhergestellt = new WissenBeitrag();
        var entwurf = UeberJson(WissenEntwurf.Aus(beitrag, slugManuell: false));
        entwurf.AnwendenAuf(wiederhergestellt, KeineFehlenden);

        entwurf.SlugManuell.ShouldBeFalse();
        entwurf.BildIds().ShouldBe([block.BildId!.Value]);
        SollGleichSein(beitrag, wiederhergestellt);
        SollGleichSein(block, wiederhergestellt.Bloecke.ShouldHaveSingleItem());
    }

    [Fact]
    public void Themen_Entwurf_aendert_vorhandene_Bausteine_entfernt_fehlende_und_legt_neue_an()
    {
        var bleibt = new WissenBlock { Id = 10, Typ = WissenBlockTyp.BildMitText, BildId = 3, TextInhalt = "alt" };
        var entfaellt = new WissenBlock { Id = 11, Typ = WissenBlockTyp.TextAbsatz };
        var beitrag = new WissenBeitrag { Id = 5, Bloecke = { bleibt, entfaellt } };

        var entwurf = new WissenEntwurf
        {
            Titel = "Neu",
            Bloecke =
            {
                new() { Id = 10, Typ = WissenBlockTyp.BildMitText, BildId = 3, TextInhalt = "geändert" },
                new() { Id = 0, Typ = WissenBlockTyp.BildEinzel, BildId = 9, Sortierung = 1 },
                // Inzwischen von jemand anderem gelöscht: entsteht neu
                new() { Id = 99, Typ = WissenBlockTyp.TextAbsatz, TextInhalt = "verwaist", Sortierung = 2 }
            }
        };

        UeberJson(entwurf).AnwendenAuf(beitrag, new HashSet<int> { 9 });

        beitrag.Bloecke.Count.ShouldBe(3);
        beitrag.Bloecke.ShouldNotContain(entfaellt);
        beitrag.Bloecke.ShouldContain(bleibt);
        bleibt.TextInhalt.ShouldBe("geändert");
        bleibt.BildId.ShouldBe(3);
        beitrag.Bloecke.Single(b => b.Typ == WissenBlockTyp.BildEinzel).BildId.ShouldBeNull("das Bild gibt es nicht mehr");
        beitrag.Bloecke.Single(b => b.TextInhalt == "verwaist").Id.ShouldBe(0);
    }
}
