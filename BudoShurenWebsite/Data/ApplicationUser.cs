using BudoShurenWebsite.Global;
using Microsoft.AspNetCore.Identity;

namespace BudoShurenWebsite.Data
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalDataAttribute]
        public string Abteilung { get; set; } = string.Empty;

        public bool Verified { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public string VerifiedBy { get; set; } = string.Empty;

        [PersonalDataAttribute]
        public string Name { get; set; } = string.Empty;

        [PersonalDataAttribute]
        public string Vorname { get; set; } = string.Empty;

    }
}
