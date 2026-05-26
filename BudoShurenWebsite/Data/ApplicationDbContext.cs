using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
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
            builder.Entity<DbImage>().HasKey(u => u.Id);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<DbImage>().ToTable("Images");
            builder.Entity<DbImage>().HasIndex(u => u.Id).IsUnique();

            // Set the primary key for the Neuigkeit entity
            builder.Entity<Neuigkeit>().HasKey(u => u.ID);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<Neuigkeit>().ToTable("Neuigkeiten");
            builder.Entity<Neuigkeit>().HasIndex(u => u.ID).IsUnique();

            builder.Entity<Neuigkeit>()
                .HasOne(n => n.DbImage)
                .WithOne(d => d.Neuigkeit)
                .HasForeignKey<Neuigkeit>(n => n.DbImageId);

            builder.Entity<GalerieEintrag>()
                .HasOne(n => n.DbImage)
                .WithOne(d => d.GalerieEintrag)
                .HasForeignKey<GalerieEintrag>(n => n.DbImageId);

            // Set the primary key for the AppointmentData entity
            builder.Entity<AppointmentData>().HasKey(u => u.Id);
            // Set the table name and unique index for the GalerieEintrag entity
            builder.Entity<AppointmentData>().ToTable("Appointments");
            builder.Entity<AppointmentData>().HasIndex(u => u.Id).IsUnique();

            // Set the primary key for the EmailSetting entity
            builder.Entity<EmailSetting>().HasKey(u => u.ID);
            // Set the table name and unique index for the EmailSetting entity
            builder.Entity<EmailSetting>().ToTable("EmailSettings");
            builder.Entity<EmailSetting>().HasIndex(u => u.ID).IsUnique();

            // Set the primary key for the Abteilung entity
            builder.Entity<Abteilung>().HasKey(u => u.ID);
            // Set the table name and unique index for the Abteilung entity
            builder.Entity<Abteilung>().ToTable("Abteilungen");
            builder.Entity<Abteilung>().HasIndex(u => u.ID).IsUnique();

            builder.Entity<Visit>().HasKey(u => u.ID);
            builder.Entity<Visit>().ToTable("Visit");
            builder.Entity<Visit>().HasIndex(u => u.ID).IsUnique();

            // WissenKategorie configuration
            builder.Entity<WissenKategorie>().HasKey(k => k.Id);
            builder.Entity<WissenKategorie>().ToTable("WissenKategorien");
            builder.Entity<WissenKategorie>().HasIndex(k => k.Id).IsUnique();
            builder.Entity<WissenKategorie>()
                .HasOne(k => k.Abteilung)
                .WithMany()
                .HasForeignKey(k => k.AbteilungId)
                .OnDelete(DeleteBehavior.SetNull);

            // WissenBeitrag configuration
            builder.Entity<WissenBeitrag>().HasKey(b => b.Id);
            builder.Entity<WissenBeitrag>().ToTable("WissenBeitraege");
            builder.Entity<WissenBeitrag>().HasIndex(b => b.Id).IsUnique();
            builder.Entity<WissenBeitrag>().HasIndex(b => b.Slug).IsUnique();
            builder.Entity<WissenBeitrag>()
                .HasOne(b => b.Kategorie)
                .WithMany(k => k.Beitraege)
                .HasForeignKey(b => b.KategorieId)
                .OnDelete(DeleteBehavior.Restrict);

            // WissenBlock configuration
            builder.Entity<WissenBlock>().HasKey(bl => bl.Id);
            builder.Entity<WissenBlock>().ToTable("WissenBloecke");
            builder.Entity<WissenBlock>().HasIndex(bl => bl.Id).IsUnique();
            builder.Entity<WissenBlock>()
                .HasOne(bl => bl.Beitrag)
                .WithMany(b => b.Bloecke)
                .HasForeignKey(bl => bl.BeitragId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<WissenBlock>()
                .HasOne(bl => bl.Bild)
                .WithMany()
                .HasForeignKey(bl => bl.BildId)
                .OnDelete(DeleteBehavior.SetNull);

            // Seed-Data: Allgemein-Kategorie
            builder.Entity<WissenKategorie>().HasData(
                new WissenKategorie
                {
                    Id = 1,
                    Name = "Allgemein",
                    Slug = "allgemein",
                    SortOrder = 0
                }
            );
        }

        public DbSet<GalerieEintrag> Galerie { get; set; }
        public DbSet<Neuigkeit> Neuigkeiten { get; set; }
        public DbSet<AppointmentData> Appointments { get; set; }
        public DbSet<EmailSetting> EmailSettings { get; set; }
        public DbSet<Abteilung> Abteilungen { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<DbImage> Images { get; set; }
        public DbSet<WissenKategorie> WissenKategorien { get; set; }
        public DbSet<WissenBeitrag> WissenBeitraege { get; set; }
        public DbSet<WissenBlock> WissenBloecke { get; set; }
        public override int SaveChanges()
        {
            Validate();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Validate();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void Validate()
        {
            var mainEntities = ChangeTracker.Entries<EmailSetting>()
                .Where(e => e.Entity.IsMain && (e.State == EntityState.Added || e.State == EntityState.Modified));

            if (mainEntities.Count() > 1 || (mainEntities.Any() && EmailSettings.Any(e => e.IsMain && !mainEntities.Any(m => m.Entity.ID == e.ID))))
            {
                throw new InvalidOperationException("Es kann nur eine Hauptentität geben.");
            }
        }

    }
}
