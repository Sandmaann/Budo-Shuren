using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudoShurenWebsite.Models
{
    public class AppointmentData : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Bitte gib einen Titel für den Termin an.")]

        public string Subject { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte gib einen Start an.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Bitte gib einen Ende an.")]
        public DateTime EndTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsAllDay { get; set; }
        public string RecurrenceRule { get; set; } = string.Empty;
        public string RecurrenceException { get; set; } = string.Empty;
        public Nullable<int> RecurrenceID { get; set; }

        [Required(ErrorMessage = "Bitte wähle eine Abteilung aus.")]
        public string Abteilung { get; set; } = string.Empty;
        public bool ShowInWeek { get; set; }
        public bool ShowInMonth { get; set; }


        public bool AlternativeColor { get; set; }

        public DateTime? Created { get; set; }
        public string EntryCreatedBy { get; set; } = string.Empty;

        public string LastChangedBy { get; set; } = string.Empty;
        public DateTime? LastChange { get; set; }

        /// <summary>
        /// Gesetzt, wenn der Eintrag zu einem Veranstaltungstag gehört. Er wird dann nur über die
        /// Veranstaltung gepflegt (KalenderEintragFabrik) und ist im Kalender schreibgeschützt.
        /// </summary>
        public int? VeranstaltungsTagId { get; set; }

        /// <summary>Wird von SfSchedule über den Standard-Feldnamen "IsReadonly" ausgewertet.</summary>
        [NotMapped]
        public bool IsReadonly => VeranstaltungsTagId != null;

        [NotMapped]
        public string PrimaryColor
        {

            get
            {
                switch (Abteilung)
                {
                    case "Verein":
                        return "#F06060";
                    case "Aikido":
                        return "#F3B062";
                    case "Bujinkan":
                        return "#93BE8C";
                    case "Genbukan":
                        return "#8CA9BE";
                    default:
                        return "#53836B";
                }
            }
        }

        [NotMapped]
        public string SecondaryColor
        {
            get
            {
                switch (Abteilung)
                {
                    case "Verein":
                        return "#f78888";
                    case "Aikido":
                        return "#f2c085";
                    case "Bujinkan":
                        return "#b5d9b0";
                    case "Genbukan":
                        return "#074973";
                    default:
                        return "#53836B";
                }
            }
        }

        [NotMapped]
        public string Color
        {
            get
            {
                if (AlternativeColor)
                    return SecondaryColor;
                return PrimaryColor;
            }
        }
        [NotMapped]
        public string CustomErrorMessage { get; set; } = string.Empty;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ShowInWeek && !ShowInMonth)
            {
                //yield return new ValidationResult(
                //    "Bitte setze entweder 'Wochenkalender' oder 'Monatskalender'. Ansonsten wird der Termin in keinem Kalender angezeigt!",
                //    new[] { nameof(ShowInWeek), nameof(ShowInMonth) });

                CustomErrorMessage = "Bitte setze entweder 'Wochenkalender' oder 'Monatskalender'. Ansonsten wird der Termin in keinem Kalender angezeigt!";
                yield return new ValidationResult(
                    CustomErrorMessage,
                    new[] { nameof(ShowInWeek), nameof(ShowInMonth) });
            }
        }

        public void CopyPropertiesFrom(AppointmentData other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            this.Id = other.Id;
            this.Subject = other.Subject ?? string.Empty;
            this.Location = other.Location ?? string.Empty;
            this.StartTime = other.StartTime;
            this.EndTime = other.EndTime;
            this.Description = other.Description ?? string.Empty;
            this.IsAllDay = other.IsAllDay;
            this.RecurrenceRule = other.RecurrenceRule ?? string.Empty;
            this.RecurrenceException = other.RecurrenceException ?? string.Empty;
            this.RecurrenceID = other.RecurrenceID;
            this.Abteilung = other.Abteilung ?? string.Empty;
            this.ShowInWeek = other.ShowInWeek;
            this.ShowInMonth = other.ShowInMonth;
            this.AlternativeColor = other.AlternativeColor;
        }

    }
}