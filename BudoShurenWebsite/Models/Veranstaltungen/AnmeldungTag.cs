namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>Gebuchter Tag einer Anmeldung (nur bei Teilnahmemodus EinzelneTage).</summary>
    public class AnmeldungTag
    {
        public int AnmeldungId { get; set; }

        public Anmeldung? Anmeldung { get; set; }

        public int VeranstaltungsTagId { get; set; }

        public VeranstaltungsTag? VeranstaltungsTag { get; set; }
    }
}
