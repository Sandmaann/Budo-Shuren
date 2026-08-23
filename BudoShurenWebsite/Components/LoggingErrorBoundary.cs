using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BudoShurenWebsite.Components
{
    public class LoggingErrorBoundary : ErrorBoundary
    {
        [Inject]
        private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

        protected override Task OnErrorAsync(Exception exception)
        {
            Logger.LogError(exception, "Unbehandelter Fehler in einer Blazor-Komponente");
            return Task.CompletedTask;
        }
    }
}
