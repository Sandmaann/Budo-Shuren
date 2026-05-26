using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    public class SlugService
    {
        public string GenerateSlug(string titel)
        {
            if (string.IsNullOrWhiteSpace(titel))
                return string.Empty;

            // Normalize to remove accents and diacritics
            var normalizedString = titel.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var slug = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            // Replace German umlauts
            slug = slug.Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
                       .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
                       .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
                       .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);

            // Convert to lowercase
            slug = slug.ToLowerInvariant();

            // Replace spaces and special characters with hyphens
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "-");

            // Remove consecutive hyphens
            slug = Regex.Replace(slug, @"-{2,}", "-");

            // Trim hyphens from start and end
            slug = slug.Trim('-');

            return slug;
        }

        public async Task<string> GenerateUniqueSlugAsync(string titel, ApplicationDbContext dbContext, int? excludeId = null)
        {
            var baseSlug = GenerateSlug(titel);
            var slug = baseSlug;
            var counter = 2;

            while (true)
            {
                var exists = await dbContext.WissenBeitraege
                    .AnyAsync(b => b.Slug == slug && (excludeId == null || b.Id != excludeId));

                if (!exists)
                    return slug;

                slug = $"{baseSlug}-{counter}";
                counter++;
            }
        }
    }
}
