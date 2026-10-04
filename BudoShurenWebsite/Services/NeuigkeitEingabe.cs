using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Services
{
    /// <summary>Formularmodell von "Neuigkeiten verwalten"; wird auch als Entwurf gesichert (Services/Entwuerfe).</summary>
    public sealed class NeuigkeitEingabe
    {
        /// <summary>Leer = neue Neuigkeit.</summary>
        public int? Id { get; set; }

        [Required(ErrorMessage = "Bitte einen Titel angeben.")]
        public string Titel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte eine Beschreibung angeben.")]
        public string Beschreibung { get; set; } = string.Empty;

        public string Ort { get; set; } = string.Empty;

        public string Linktext { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;

        public string Quellenangabe { get; set; } = string.Empty;

        public DateTime? Datum { get; set; }

        public int Sortierung { get; set; }

        public bool IstStandardneuigkeit { get; set; }

        public DateTime? Ablaufdatum { get; set; }

        /// <summary>Das Bild lässt sich nur beim Anlegen wählen.</summary>
        public int? DbImageId { get; set; }

        public static NeuigkeitEingabe Aus(Neuigkeit n) => new()
        {
            Id = n.ID,
            Titel = n.Titel,
            Beschreibung = n.Beschreibung,
            Ort = n.Ort,
            Linktext = n.Linktext,
            Link = n.Link,
            Quellenangabe = n.Quellenangabe,
            Datum = n.Datum,
            Sortierung = n.Sortierung,
            IstStandardneuigkeit = n.IstStandardneuigkeit,
            Ablaufdatum = n.Ablaufdatum,
            DbImageId = n.DbImageId
        };

        /// <summary>Legt die Neuigkeit an oder ändert sie. Rechte prüft der Aufrufer.</summary>
        /// <returns>null bei Erfolg, sonst die Meldung für den Benutzer.</returns>
        public async Task<string?> SpeichernAsync(ApplicationDbContext kontext, string benutzerName, CancellationToken abbruch = default)
        {
            Neuigkeit neuigkeit;
            if (Id is { } id)
            {
                var vorhanden = await kontext.Neuigkeiten.FirstOrDefaultAsync(n => n.ID == id, abbruch);
                if (vorhanden is null)
                    return "Die Neuigkeit gibt es nicht mehr.";
                neuigkeit = vorhanden;
            }
            else
            {
                if (DbImageId is not { } bildId)
                    return "Bitte ein Bild hochladen.";
                if (!await kontext.Images.AnyAsync(i => i.Id == bildId, abbruch))
                    return "Das Bild gibt es nicht mehr. Bitte noch einmal hochladen.";

                neuigkeit = new Neuigkeit { DbImageId = bildId, Created = DateTime.Now, EntryCreatedBy = benutzerName };
                kontext.Neuigkeiten.Add(neuigkeit);
            }

            neuigkeit.Titel = Titel;
            neuigkeit.Beschreibung = Beschreibung;
            neuigkeit.Ort = Ort ?? string.Empty;
            neuigkeit.Linktext = Linktext ?? string.Empty;
            neuigkeit.Link = Link ?? string.Empty;
            neuigkeit.Quellenangabe = Quellenangabe ?? string.Empty;
            neuigkeit.Datum = Datum;
            neuigkeit.Sortierung = Sortierung;
            neuigkeit.IstStandardneuigkeit = IstStandardneuigkeit;
            neuigkeit.Ablaufdatum = IstStandardneuigkeit ? null : Ablaufdatum;
            neuigkeit.LastChange = DateTime.Now;
            neuigkeit.LastChangedBy = benutzerName;

            // Eine Transaktion: das Bild gilt genau dann als gespeichert, wenn die Neuigkeit gespeichert ist
            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await kontext.SaveChangesAsync(abbruch);
            if (neuigkeit.DbImageId is { } gespeichertesBild)
                await BildVerwendung.AlsGespeichertMarkierenAsync(kontext, [gespeichertesBild], abbruch);
            await transaktion.CommitAsync(abbruch);
            return null;
        }
    }
}
