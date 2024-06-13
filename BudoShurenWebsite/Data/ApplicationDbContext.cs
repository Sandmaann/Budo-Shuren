using BudoShurenWebsite.Models;
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

            // Set the primary key for the GalerieEintrag entity
            builder.Entity<GalerieEintrag>().HasKey(u => u.ID);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<GalerieEintrag>().ToTable("Galerie");
            builder.Entity<GalerieEintrag>().HasIndex(u => u.ID).IsUnique();

            // Set the primary key for the Neuigkeit entity
            builder.Entity<Neuigkeit>().HasKey(u => u.ID);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<Neuigkeit>().ToTable("Neuigkeiten");
            builder.Entity<Neuigkeit>().HasIndex(u => u.ID).IsUnique();

            // Set the primary key for the AppointmentData entity
            builder.Entity<AppointmentData>().HasKey(u => u.Id);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<AppointmentData>().ToTable("Appointments");
            builder.Entity<AppointmentData>().HasIndex(u => u.Id).IsUnique();
        }

        public DbSet<GalerieEintrag> Galerie { get; set; }
        public DbSet<Neuigkeit> Neuigkeiten { get; set; }
        public DbSet<AppointmentData> Appointments { get; set; }

    }
}
