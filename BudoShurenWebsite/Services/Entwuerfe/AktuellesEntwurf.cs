using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Entwuerfe
{
    /// <summary>
    /// Ungespeicherter Stand des Aktuelles-Editors (siehe EntwurfSicherung). Der Editor bearbeitet direkt den Beitrag
    /// aus der Datenbank; der Entwurf hält nur, was im Editor änderbar ist. Neue Felder des Editors hier ergänzen
    /// (AktuellesEntwurfTests schlägt sonst fehl).
    /// </summary>
    public sealed class AktuellesEntwurf
    {
        public string Titel { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? AbteilungId { get; set; }
        public bool Veroeffentlicht { get; set; }
        public DateTime Datum { get; set; }
        public string MetaTitel { get; set; } = string.Empty;
        public string MetaBeschreibung { get; set; } = string.Empty;

        /// <summary>Zustand des Editors: der Slug folgt nicht mehr dem Titel.</summary>
        public bool SlugManuell { get; set; }

        public List<Block> Bloecke { get; set; } = [];

        public sealed class Block
        {
            /// <summary>0 = im Editor neu angelegt.</summary>
            public int Id { get; set; }
            public AktuellesBlockTyp Typ { get; set; }
            public int Sortierung { get; set; }
            public string? MarkdownInhalt { get; set; }
            public int BilderProReihe { get; set; }
            public string? BildUnterschrift { get; set; }
            public List<Bild> Bilder { get; set; } = [];
        }

        public sealed class Bild
        {
            /// <summary>0 = im Editor neu hochgeladen.</summary>
            public int Id { get; set; }
            public int BildId { get; set; }
            public int Sortierung { get; set; }
        }

        public IEnumerable<int> BildIds() => Bloecke.SelectMany(b => b.Bilder).Select(b => b.BildId);

        public static AktuellesEntwurf Aus(AktuellesBeitrag beitrag, bool slugManuell) => new()
        {
            Titel = beitrag.Titel,
            Slug = beitrag.Slug,
            AbteilungId = beitrag.AbteilungId,
            Veroeffentlicht = beitrag.Veroeffentlicht,
            Datum = beitrag.Datum,
            MetaTitel = beitrag.MetaTitel,
            MetaBeschreibung = beitrag.MetaBeschreibung,
            SlugManuell = slugManuell,
            Bloecke = beitrag.Bloecke.OrderBy(b => b.Sortierung).Select(b => new Block
            {
                Id = b.Id,
                Typ = b.Typ,
                Sortierung = b.Sortierung,
                MarkdownInhalt = b.MarkdownInhalt,
                BilderProReihe = b.BilderProReihe,
                BildUnterschrift = b.BildUnterschrift,
                Bilder = b.Bilder.OrderBy(i => i.Sortierung).Select(i => new Bild { Id = i.Id, BildId = i.BildId, Sortierung = i.Sortierung }).ToList()
            }).ToList()
        };

        /// <summary>
        /// Überträgt den Entwurf auf den geladenen Beitrag, so wie es die Schritte im Editor getan hätten:
        /// vorhandene Bausteine und Bilder werden geändert, fehlende entfernt, neue angelegt.
        /// </summary>
        /// <param name="fehlendeBilder">Bilder, die es nicht mehr gibt (IEntwurfSpeicher.FehlendeBilderAsync): sie werden ausgelassen.</param>
        public void AnwendenAuf(AktuellesBeitrag beitrag, IReadOnlySet<int> fehlendeBilder)
        {
            beitrag.Titel = Titel;
            beitrag.Slug = Slug;
            beitrag.AbteilungId = AbteilungId;
            beitrag.Veroeffentlicht = Veroeffentlicht;
            beitrag.Datum = Datum;
            beitrag.MetaTitel = MetaTitel;
            beitrag.MetaBeschreibung = MetaBeschreibung;

            var vorhandeneBloecke = beitrag.Bloecke.Where(b => b.Id != 0).ToDictionary(b => b.Id);
            beitrag.Bloecke.Clear();
            foreach (var entwurf in Bloecke)
            {
                // Inzwischen von jemand anderem gelöschte Bausteine entstehen neu
                var block = vorhandeneBloecke.GetValueOrDefault(entwurf.Id) ?? new AktuellesBlock();
                block.Typ = entwurf.Typ;
                block.Sortierung = entwurf.Sortierung;
                block.MarkdownInhalt = entwurf.MarkdownInhalt;
                block.BilderProReihe = entwurf.BilderProReihe;
                block.BildUnterschrift = entwurf.BildUnterschrift;

                var vorhandeneBilder = block.Bilder.Where(i => i.Id != 0).ToDictionary(i => i.Id);
                block.Bilder.Clear();
                foreach (var bild in entwurf.Bilder.Where(i => !fehlendeBilder.Contains(i.BildId)))
                {
                    var eintrag = vorhandeneBilder.GetValueOrDefault(bild.Id) ?? new AktuellesBild { BlockId = block.Id };
                    eintrag.BildId = bild.BildId;
                    eintrag.Sortierung = bild.Sortierung;
                    block.Bilder.Add(eintrag);
                }

                beitrag.Bloecke.Add(block);
            }
        }
    }
}
