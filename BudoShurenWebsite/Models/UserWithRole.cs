using BudoShurenWebsite.Data;
using System.Diagnostics.CodeAnalysis;

namespace BudoShurenWebsite.Models
{
    public class UserWithRoles
    {
        public ApplicationUser User { get; set; }

        public string Vorname { get => User.Vorname; set => User.Vorname = value; }
        public string Name { get => User.Name; set => User.Name = value; }
        public string Abteilung { get => User.Abteilung; set => User.Abteilung = value; }
        public string UserName { get => User.UserName ?? "unbekannt"; set => User.UserName = value; }
        public string EmailConfirmed { get => User.EmailConfirmed ? "Ja" : "Nein"; }
        public string Verified
        {
            get => User.Verified ? "Ja" : "Nein";
            set
            {
                if (value.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
                    User.Verified = true;
                else
                    User.Verified = false;
            }
        }


        public IList<string> Roles { get; set; } = new List<string>();

        public string Rolle
        {
            get
            {
                if (this.Roles?.Any() == true)
                    return Roles.First();
                else
                    return "Unbekannt";
            }
            set
            {
                if (value == null)
                    return;
                if (Global.Roles.AllRoles.Contains(value))
                {
                    if (Rolle != value)
                        Roles = new List<string> { value };
                }
                else
                    throw new Exception("Rolle nicht bekannt");
            }
        }



    }
}
