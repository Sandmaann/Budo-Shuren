using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    public class ImageService
    {
        private readonly ApplicationDbContext _context;

        public ImageService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <param name="vorlaeufig">Bild aus einem Editor, der erst beim Speichern übernimmt (DbImage.VorlaeufigSeitUtc).</param>
        public async Task<int> UploadImageAsync(string title, byte[] imageData, string contentType, bool vorlaeufig = false)
        {
            var jetzt = DateTime.UtcNow;
            var image = new DbImage
            {
                Title = title,
                ImageData = imageData,
                ContentType = contentType,
                CreatedAt = jetzt,
                VorlaeufigSeitUtc = vorlaeufig ? jetzt : null
            };

            _context.Images.Add(image);
            await _context.SaveChangesAsync();
            return image.Id;
        }

        public async Task<bool> RemoveImageAsync(string title, string contentType)
        {
            //Get Image by Title and contentType
            var image = await _context.Images.FirstOrDefaultAsync(x => x.Title == title && x.ContentType == contentType);
            if(image != null)
            {
                _context.Images.Remove(image);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> RemoveImageAsync(int imageId)
        {
            //Get Image by Title and contentType
            var image = await _context.Images.FindAsync(imageId);
            if (image != null)
            {
                _context.Images.Remove(image);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        /// <summary>Angaben zu einem Bild ohne die Bilddaten: reicht für ETag und Cache-Header.</summary>
        public async Task<BildKopf?> GetBildKopfAsync(int id)
        {
            return await _context.Images
                .Where(x => x.Id == id)
                .Select(x => new BildKopf(x.ContentType, x.CreatedAt))
                .FirstOrDefaultAsync();
        }

        public async Task<byte[]?> GetImageDataAsync(int id)
        {
            return await _context.Images
                .Where(x => x.Id == id)
                .Select(x => x.ImageData)
                .FirstOrDefaultAsync();
        }

        /// <summary>Bilder der Galerie sollen nicht in der Bildersuche erscheinen (auch wenn sie öffentlich sind).</summary>
        public async Task<bool> IstGalerieBildAsync(int id)
        {
            return await _context.Galerie.AnyAsync(x => x.DbImageId == id);
        }

        public async Task<bool> AllowAnonymous(int id)
        {
            //Prüfen ob es eine neuigkeit ist
            if (await _context.Neuigkeiten.AnyAsync(x => x.DbImageId == id))
                return true;

            //Prüfen ob es ein öffentlicher galerie eintrag ist
            if (await _context.Galerie.AnyAsync(x => x.DbImageId == id && x.Öffentlich))
                return true;

            //Prüfen ob es ein Bild in einem veröffentlichten Wissens-Beitrag ist
            if (await _context.WissenBloecke.AnyAsync(b => b.BildId == id && b.Beitrag != null && b.Beitrag.Veroeffentlicht))
                return true;

            //Prüfen ob es ein Bild einer erreichbaren Veranstaltung ist (gleiche Regel wie die Veranstaltungsseite)
            if (await _context.VeranstaltungBilder.AnyAsync(b => b.BildId == id
                    && VeranstaltungAnzeigeService.Sichtbar.Contains(b.Block!.Veranstaltung!.Status)))
                return true;

            return false;
        }
    }

    public sealed record BildKopf(string ContentType, DateTime CreatedAt);
}