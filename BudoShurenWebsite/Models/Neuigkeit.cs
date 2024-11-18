using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudoShurenWebsite.Models
{
    public class Neuigkeit
    {
        [Key]
        public int ID { get; set; }
        public string Titel { get; set; } = string.Empty;
        public string Beschreibung { get; set; } = string.Empty;
        public string Ort { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Linktext { get; set; } = string.Empty;
        public string Quellenangabe { get; set; } = string.Empty;
        public DateTime? Datum { get; set; }
        public int Sortierung { get; set; }
        public DateTime? Created { get; set; }
        public string EntryCreatedBy { get; set; } = string.Empty;

        public string LastChangedBy { get; set; } = string.Empty;
        public DateTime? LastChange { get; set; }
        public int? DbImageId { get; set; }
        public DbImage? DbImage { get; set; }

        [NotMapped]
        public bool IsLoading { get; set; } = true;
        [NotMapped]
        public bool ShowErrorImage { get; set; } = false;

        public void OnImageLoaded()
        {
            IsLoading = false;
            if (StateHasChanged != null)
                StateHasChanged();
        }
        public void OnImageError()
        {
            IsLoading = false;
            ShowErrorImage = true;
            if (StateHasChanged != null)
                StateHasChanged();
        }

        [NotMapped]
        public Action? StateHasChanged { get; set; }
    }
}
