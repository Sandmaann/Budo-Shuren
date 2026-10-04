namespace BudoShurenWebsite.Services.Entwuerfe
{
    /// <summary>Ungespeicherter Stand der Seitenleiste von "Galerie verwalten" (EditGalerie, siehe EntwurfSicherung).</summary>
    public sealed class GalerieEntwurf
    {
        /// <summary>Die bearbeiteten Einträge; leer = es werden neue Bilder angelegt.</summary>
        public List<int> EintragIds { get; set; } = [];

        /// <summary>Für neue Einträge hochgeladene Bilder, zu denen es noch keinen Eintrag gibt.</summary>
        public List<HochgeladenesBild> Bilder { get; set; } = [];

        public string Titel { get; set; } = string.Empty;
        public string Untertitel { get; set; } = string.Empty;
        public string Beschreibung { get; set; } = string.Empty;
        public string ImageCreatedBy { get; set; } = string.Empty;
        public DateTime? ImageDate { get; set; }
        public bool Oeffentlich { get; set; }
        public bool Home { get; set; }
        public bool Aikido { get; set; }
        public bool Bujinkan { get; set; }
        public bool Genbukan { get; set; }
        public bool Iaido { get; set; }
        public bool Jodo { get; set; }
    }
}
