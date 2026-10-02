namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Was zur Anzeige eines Termins gebraucht wird (Ortszeit). Umgesetzt vom Datenmodell und den Anzeige-Records,
    /// damit Seiten, Mails und Exporte dieselbe Formatierung nutzen (Services/Veranstaltungen/TerminText).
    /// </summary>
    public interface ITermin
    {
        DateOnly Datum { get; }

        TimeOnly Beginn { get; }

        /// <summary>null = offenes Ende ("ab 19:00 Uhr").</summary>
        TimeOnly? Ende { get; }

        string? Titel { get; }

        bool Abgesagt { get; }
    }
}
