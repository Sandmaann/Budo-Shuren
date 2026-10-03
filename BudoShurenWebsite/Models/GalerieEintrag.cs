using Org.BouncyCastle.Security;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudoShurenWebsite.Models
{
    [Flags]
    public enum GalerieTyp
    {
        None = 0,
        Home = 1,
        Aikido = 2,
        Bujinkan = 4,
        Genbukan = 8,
        Iaido = 16,
        Jodo = 32
    }

    public class GalerieEintrag
    {
        [Key]
        public int ID { get; set; }
        public string? Titel { get; set; }
        public string? Untertitel { get; set; }
        public string? Beschreibung { get; set; }

        public bool Öffentlich { get; set; }
        public bool Home { get; set; }
        public bool Aikido { get; set; }
        public bool Bujinkan { get; set; }
        public bool Genbukan { get; set; }
        public bool Iaido { get; set; }
        public bool Jodo { get; set; }

        public DateTime? ImageDate { get; set; }
        public string? ImageCreatedBy { get; set; }


        public DateTime? EntryCreationDateUTC { get; set; }
        public string? EntryCreatedBy { get; set; }

        public DateTime? LastChangedUTC { get; set; }
        public string? LastChangedBy { get; set; }

        [NotMapped]
        public DateTime? SortDate => ImageDate ?? EntryCreationDateUTC;

        [NotMapped]
        public bool IsSelected { get; set; }

        [NotMapped]
        public bool SelectionMode { get; set; }

        [NotMapped]
        public GalerieTyp Typ
        {
            get
            {
                GalerieTyp typ = GalerieTyp.None;

                if (Home)
                    typ |= GalerieTyp.Home;
                if (Aikido)
                    typ |= GalerieTyp.Aikido;
                if (Bujinkan)
                    typ |= GalerieTyp.Bujinkan;
                if (Genbukan)
                    typ |= GalerieTyp.Genbukan;
                if (Iaido)
                    typ |= GalerieTyp.Iaido;
                if (Jodo)
                    typ |= GalerieTyp.Jodo;
                return typ;
            }
        }

        // Navigation property
        public int? DbImageId { get; set; }
        public DbImage? DbImage { get; set; }


        /// <summary>
        /// Maße der Kachel, um ihren Platz freizuhalten, bevor das Bild geladen ist (BildVariantenService.KachelMasseSetzenAsync).
        /// Null, solange die Maße des Bildes noch nicht bekannt sind.
        /// </summary>
        [NotMapped]
        public int? KachelBreite { get; set; }
        [NotMapped]
        public int? KachelHoehe { get; set; }

        [NotMapped]
        public bool IsLoading { get; set; } = true;
        [NotMapped]
        public bool ShowErrorImage { get; set; } = false;

        public void OnImageLoaded()
        {
            IsLoading = false;
            if (StateHasChanged != null)
                StateHasChanged();
        }
        public void OnImageError()
        {
            IsLoading = false;
            ShowErrorImage = true;
            if (StateHasChanged != null)
                StateHasChanged();
        }

        [NotMapped]
        public Action? StateHasChanged { get; set; }

        [NotMapped]
        public bool DisableSelection { get; set; }

        [NotMapped]
        public bool DisableHover { get; set; }
    }
}
