using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>Eine Mail, die über IEmailWarteschlange eingereiht werden soll.</summary>
    /// <param name="An">Empfängeradresse (nur die Adresse, ohne Anzeigenamen).</param>
    /// <param name="Html">Fertiges HTML. Benutzerwerte müssen vorher HTML-kodiert sein.</param>
    public sealed record AusgehendeEmail(string An, string Betreff, string Html)
    {
        /// <summary>Reply-To, z. B. die Kontaktadresse einer Veranstaltung.</summary>
        public string? AntwortAn { get; init; }

        public EmailPrioritaet Prioritaet { get; init; } = EmailPrioritaet.Normal;

        public IReadOnlyList<EmailAnhang> Anhaenge { get; init; } = [];

        public string? BezugTyp { get; init; }

        public int? BezugId { get; init; }
    }

    public sealed record EmailAnhang(string Dateiname, string MimeTyp, byte[] Inhalt);
}
