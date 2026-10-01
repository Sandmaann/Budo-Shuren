using System.ComponentModel;
using System.Reflection;

namespace BudoShurenWebsite.Global
{
    public static class EnumText
    {
        /// <summary>Der deutsche Anzeigetext aus [Description], sonst der Name des Werts.</summary>
        public static string Beschreibung(this Enum wert) =>
            wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
            ?? wert.ToString();
    }
}
