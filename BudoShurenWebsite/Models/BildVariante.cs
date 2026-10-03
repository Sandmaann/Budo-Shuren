using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    /// <summary>Wofür eine verkleinerte Fassung eines Bildes gedacht ist. Die Maße dazu stehen in BildZuschnitt.</summary>
    public enum BildVariantenArt
    {
        /// <summary>Neuigkeiten auf der Startseite: festes Format je nach Hoch- oder Querformat.</summary>
        Neuigkeit = 1,

        /// <summary>Kachel in den Galerien (ImageCard): verkleinert, nicht zugeschnitten. Die große Ansicht zeigt das Original.</summary>
        GalerieKachel = 2
    }

    /// <summary>
    /// Verkleinerte, zugeschnittene Fassung eines Bildes (DbImage). Sie entsteht beim ersten Abruf und bleibt gespeichert,
    /// damit sie nur einmal berechnet wird (siehe BildVariantenService). Mit dem Bild wird sie gelöscht.
    /// </summary>
    public class BildVariante
    {
        [Key]
        public int Id { get; set; }
        public int BildId { get; set; }
        public BildVariantenArt Art { get; set; }
        public int Breite { get; set; }
        public int Hoehe { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public byte[] Daten { get; set; } = [];
        public DateTime ErstelltUtc { get; set; }
    }
}
