using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Controllers
{
    /// <summary>
    /// Download der Teilnehmerliste als CSV. Als Controller, weil eine interaktive Blazor-Seite keine Datei ausliefern kann.
    /// Neben den Rollen prüft der Service, ob der Benutzer genau diese Veranstaltung verwalten darf.
    /// Bewusst ohne [ApiController]: Sonst antwortet das Identity-Cookie seit .NET 10 mit 401 statt zum Login weiterzuleiten.
    /// </summary>
    [Authorize]
    [Authorize(Policy = "NotGuest")]
    [Authorize(Policy = "Aktiviert")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Abteilungsleiter}")]
    [Route("Account/Member/Veranstaltungen")]
    public class VeranstaltungExportController : ControllerBase
    {
        private readonly ITeilnehmerVerwaltungService _teilnehmer;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<VeranstaltungenOptionen> _optionen;

        public VeranstaltungExportController(ITeilnehmerVerwaltungService teilnehmer, UserManager<ApplicationUser> userManager, IOptions<VeranstaltungenOptionen> optionen)
        {
            _teilnehmer = teilnehmer;
            _userManager = userManager;
            _optionen = optionen;
        }

        [HttpGet("{id:int}/teilnehmer.csv")]
        public async Task<IActionResult> TeilnehmerCsv(int id, CancellationToken abbruch)
        {
            if (!_optionen.Value.Aktiviert)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Forbid();
            var benutzer = VerwaltungsBenutzer.Aus(new UserWithRoles { User = user, Roles = await _userManager.GetRolesAsync(user) });

            // null: Veranstaltung gibt es nicht oder sie gehört zu einer anderen Abteilung – bewusst nicht unterschieden
            if (benutzer is null || await _teilnehmer.CsvExportAsync(id, benutzer, abbruch) is not { } export)
                return NotFound();

            return File(export.Inhalt, "text/csv; charset=utf-8", export.Dateiname);
        }
    }
}
