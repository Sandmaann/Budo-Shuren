namespace BudoShurenWebsite.Global
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Abteilungsleiter = "Abteilungsleiter";
        public const string Editor = "Editor";
        public const string Mitglied = "Mitglied";
        public const string Gast = "Gast";

        public static string[] AllRoles = new string[] { Admin, Abteilungsleiter, Editor, Mitglied, Gast };
    
        public static bool IsAdmin(IList<string> roles) => roles.Contains(Admin);
        public static bool IsAbteilungsleiter(IList<string> roles) => roles.Contains(Abteilungsleiter);
        public static bool IsEditor(IList<string> roles) => roles.Contains(Editor);
        public static bool IsMitglied(IList<string> roles) => roles.Contains(Mitglied);
        public static bool IsGast(IList<string> roles) => roles.Contains(Gast);
    }
}
