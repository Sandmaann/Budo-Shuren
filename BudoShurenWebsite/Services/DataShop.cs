using BudoShurenWebsite.Models;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BudoShurenWebsite.Services
{
    public interface IDataService
    {
        Task<IEnumerable<Abteilung>> GetAbteilungenAsync();
    }

    public class DataService : IDataService
    {
        /// <summary>
        /// Ungünstig für Produktion, das wird einmal beim Start gemacht und wird nicht aktualisiert.
        /// </summary>
        private IEnumerable<Abteilung> abteilungen = Array.Empty<Abteilung>();

        public DataService()
        {
            InitializeDemoData();
        }

        private void InitializeDemoData()
        {
            this.abteilungen = new List<Abteilung> {
                new Abteilung() { ID= "Aikido", Name= "Aikido", Abteilungsleiter = "(Karin Oetzel)" },
                new Abteilung() { ID= "Bujinkan", Name= "Bujinkan", Abteilungsleiter = "(Johannes Schiebl)" },
                new Abteilung() { ID= "Genbukan", Name= "Genbukan", Abteilungsleiter = "(Henry Schubert)" },
              };
        }

        public Task<IEnumerable<Abteilung>> GetAbteilungenAsync()
        {
            return Task.FromResult(abteilungen);
        }
    }
}