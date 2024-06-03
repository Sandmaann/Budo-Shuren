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
    }
}
