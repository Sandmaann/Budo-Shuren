using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Entwuerfe
{
    /// <summary>
    /// Ungespeicherter Stand des Themen-Editors (siehe EntwurfSicherung), gleiche Idee wie <see cref="AktuellesEntwurf"/>.
    /// Neue Felder des Editors hier ergänzen (WissenEntwurfTests schlägt sonst fehl).
    /// </summary>
    public sealed class WissenEntwurf
    {
        public string Titel { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int KategorieId { get; set; }
        public string MetaTitel { get; set; } = string.Empty;
        public string MetaBeschreibung { get; set; } = string.Empty;
        public bool Veroeffentlicht { get; set; }
        public bool AbteilungLink { get; set; }
        public int SortOrder { get; set; }
        public string HeadlineTitel { get; set; } = string.Empty;
        public string HeadlineAnekdote { get; set; } = string.Empty;

        /// <summary>Zustand des Editors: der Slug folgt nicht mehr dem Titel.</summary>
        public bool SlugManuell { get; set; }

        public List<Block> Bloecke { get; set; } = [];

        public sealed class Block
        {
            /// <summary>0 = im Editor neu angelegt.</summary>
            public int Id { get; set; }
            public WissenBlockTyp Typ { get; set; }
            public int Sortierung { get; set; }
            public string? TextInhalt { get; set; }
            public string? TextInhalt2 { get; set; }
            public string? UntertitelText { get; set; }
            public int? BildId { get; set; }
            public string? AltText { get; set; }
            public string BildPosition { get; set; } = "links";
            public string? ListenItemsJson { get; set; }
            public string? BannerTitel { get; set; }
            public bool BannerAusblenden { get; set; }
        }

        public IEnumerable<int> BildIds() => Bloecke.Where(b => b.BildId.HasValue).Select(b => b.BildId!.Value);

        public static WissenEntwurf Aus(WissenBeitrag beitrag, bool slugManuell) => new()
        {
            Titel = beitrag.Titel,
            Slug = beitrag.Slug,
            KategorieId = beitrag.KategorieId,
            MetaTitel = beitrag.MetaTitel,
            MetaBeschreibung = beitrag.MetaBeschreibung,
            Veroeffentlicht = beitrag.Veroeffentlicht,
            AbteilungLink = beitrag.AbteilungLink,
            SortOrder = beitrag.SortOrder,
            HeadlineTitel = beitrag.HeadlineTitel,
            HeadlineAnekdote = beitrag.HeadlineAnekdote,
            SlugManuell = slugManuell,
            Bloecke = beitrag.Bloecke.OrderBy(b => b.Sortierung).Select(b => new Block
            {
                Id = b.Id,
                Typ = b.Typ,
                Sortierung = b.Sortierung,
                TextInhalt = b.TextInhalt,
                TextInhalt2 = b.TextInhalt2,
                UntertitelText = b.UntertitelText,
                BildId = b.BildId,
                AltText = b.AltText,
                BildPosition = b.BildPosition,
                ListenItemsJson = b.ListenItemsJson,
                BannerTitel = b.BannerTitel,
                BannerAusblenden = b.BannerAusblenden
            }).ToList()
        };

        /// <summary>Überträgt den Entwurf auf den geladenen Beitrag: Bausteine werden geändert, fehlende entfernt, neue angelegt.</summary>
        /// <param name="fehlendeBilder">Bilder, die es nicht mehr gibt (IEntwurfSpeicher.FehlendeBilderAsync): der Baustein bleibt ohne Bild.</param>
        public void AnwendenAuf(WissenBeitrag beitrag, IReadOnlySet<int> fehlendeBilder)
        {
            beitrag.Titel = Titel;
            beitrag.Slug = Slug;
            beitrag.KategorieId = KategorieId;
            beitrag.MetaTitel = MetaTitel;
            beitrag.MetaBeschreibung = MetaBeschreibung;
            beitrag.Veroeffentlicht = Veroeffentlicht;
            beitrag.AbteilungLink = AbteilungLink;
            beitrag.SortOrder = SortOrder;
            beitrag.HeadlineTitel = HeadlineTitel;
            beitrag.HeadlineAnekdote = HeadlineAnekdote;

            var vorhanden = beitrag.Bloecke.Where(b => b.Id != 0).ToDictionary(b => b.Id);
            beitrag.Bloecke.Clear();
            foreach (var entwurf in Bloecke)
            {
                // Inzwischen von jemand anderem gelöschte Bausteine entstehen neu
                var block = vorhanden.GetValueOrDefault(entwurf.Id) ?? new WissenBlock();
                block.Typ = entwurf.Typ;
                block.Sortierung = entwurf.Sortierung;
                block.TextInhalt = entwurf.TextInhalt;
                block.TextInhalt2 = entwurf.TextInhalt2;
                block.UntertitelText = entwurf.UntertitelText;
                block.BildId = entwurf.BildId is { } bildId && !fehlendeBilder.Contains(bildId) ? bildId : null;
                block.AltText = entwurf.AltText;
                block.BildPosition = entwurf.BildPosition;
                block.ListenItemsJson = entwurf.ListenItemsJson;
                block.BannerTitel = entwurf.BannerTitel;
                block.BannerAusblenden = entwurf.BannerAusblenden;
                beitrag.Bloecke.Add(block);
            }
        }
    }
}
