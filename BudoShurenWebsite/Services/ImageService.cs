using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
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

        public async Task<int> UploadImageAsync(string title, byte[] imageData, string contentType)
        {
            var image = new DbImage
            {
                Title = title,
                ImageData = imageData,
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow
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

        public async Task<DbImage> GetImageAsync(int id)
        {
            return await _context.Images.FindAsync(id);
        }

        public async Task<bool> AllowAnonymous(int id)
        {
            //Prüfen ob es eine neuigkeit ist
            var neuigkeit = await _context.Neuigkeiten.FirstOrDefaultAsync(x => x.DbImageId == id);
            if (neuigkeit != null)
                return true;

            //Prüfen ob es ein öffentlicher galerie eintrag ist
            var galerieEintrag = await _context.Galerie.FirstOrDefaultAsync(x => x.DbImageId == id && x.Öffentlich);
            if (galerieEintrag != null)
                return true;

            //Prüfen ob es ein Bild in einem veröffentlichten Wissens-Beitrag ist
            var wissenBlock = await _context.WissenBloecke
                .Include(b => b.Beitrag)
                .FirstOrDefaultAsync(b => b.BildId == id && b.Beitrag != null && b.Beitrag.Veroeffentlicht);
            if (wissenBlock != null)
                return true;

            return false;
        }
    }
}