using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BudoShurenWebsite.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //Kunden Tabelle-Indexe erstellen
            builder.Entity<GalerieEintrag>().ToTable("Galerie");
            builder.Entity<GalerieEintrag>().HasIndex(u => u.ID).IsUnique();
        }

        public DbSet<GalerieEintrag> Galerie { get; set; }

    }
}
