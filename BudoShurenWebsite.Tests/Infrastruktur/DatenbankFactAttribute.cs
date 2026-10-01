using System.Runtime.CompilerServices;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Wie [Fact], wird aber mit Hinweis übersprungen, wenn kein SQL Server für Tests verfügbar ist.
/// </summary>
public sealed class DatenbankFactAttribute : FactAttribute
{
    public DatenbankFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!TestDatenbank.Verfuegbar)
            Skip = TestDatenbank.Hinweis;
    }
}
