using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Veranstaltungen;
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

            // AktuellesBeitrag configuration
            builder.Entity<AktuellesBeitrag>().HasKey(b => b.Id);
            builder.Entity<AktuellesBeitrag>().ToTable("AktuellesBeitraege");
            builder.Entity<AktuellesBeitrag>().HasIndex(b => b.Id).IsUnique();
            builder.Entity<AktuellesBeitrag>().HasIndex(b => b.Slug).IsUnique();
            builder.Entity<AktuellesBeitrag>()
                .HasOne(b => b.Abteilung)
                .WithMany()
                .HasForeignKey(b => b.AbteilungId)
                .OnDelete(DeleteBehavior.SetNull);

            // AktuellesBlock configuration
            builder.Entity<AktuellesBlock>().HasKey(bl => bl.Id);
            builder.Entity<AktuellesBlock>().ToTable("AktuellesBloecke");
            builder.Entity<AktuellesBlock>().HasIndex(bl => bl.Id).IsUnique();
            builder.Entity<AktuellesBlock>()
                .HasOne(bl => bl.Beitrag)
                .WithMany(b => b.Bloecke)
                .HasForeignKey(bl => bl.BeitragId)
                .OnDelete(DeleteBehavior.Cascade);

            // AktuellesBild configuration
            builder.Entity<AktuellesBild>().HasKey(bi => bi.Id);
            builder.Entity<AktuellesBild>().ToTable("AktuellesBilder");
            builder.Entity<AktuellesBild>().HasIndex(bi => bi.Id).IsUnique();
            builder.Entity<AktuellesBild>()
                .HasOne(bi => bi.Block)
                .WithMany(bl => bl.Bilder)
                .HasForeignKey(bi => bi.BlockId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<AktuellesBild>()
                .HasOne(bi => bi.Bild)
                .WithMany()
                .HasForeignKey(bi => bi.BildId)
                .OnDelete(DeleteBehavior.Cascade);

            // EmailAusgang configuration (Mail-Warteschlange, siehe Services/Mail)
            builder.Entity<EmailAusgang>().HasKey(m => m.Id);
            builder.Entity<EmailAusgang>().ToTable("EmailAusgang");
            // Abfrage des Versand-Jobs: wartende, fällige Mails nach Priorität
            builder.Entity<EmailAusgang>().HasIndex(m => new { m.Status, m.Prioritaet, m.FaelligAbUtc });

            KonfiguriereVeranstaltungen(builder);
        }

        // Modul Veranstaltungen (Models/Veranstaltungen)
        private static void KonfiguriereVeranstaltungen(ModelBuilder builder)
        {
            builder.Entity<Veranstaltung>().ToTable("Veranstaltungen");
            builder.Entity<Veranstaltung>().HasIndex(v => v.Slug).IsUnique();
            builder.Entity<Veranstaltung>()
                .HasOne(v => v.Abteilung)
                .WithMany()
                .HasForeignKey(v => v.AbteilungId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<Veranstaltung>()
                .HasOne(v => v.Bild)
                .WithMany()
                .HasForeignKey(v => v.BildId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<VeranstaltungsTag>().ToTable("VeranstaltungsTage");
            // Mehrere Termine pro Datum möglich (z. B. Training und Essen), deshalb nicht eindeutig
            builder.Entity<VeranstaltungsTag>().HasIndex(t => new { t.VeranstaltungId, t.Datum, t.Beginn });
            builder.Entity<VeranstaltungsTag>()
                .HasOne(t => t.Veranstaltung)
                .WithMany(v => v.Tage)
                .HasForeignKey(t => t.VeranstaltungId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Anmeldung>().ToTable("Anmeldungen");
            builder.Entity<Anmeldung>().HasIndex(a => new { a.VeranstaltungId, a.Email }).IsUnique();
            builder.Entity<Anmeldung>().HasIndex(a => a.TokenHash).IsUnique();
            builder.Entity<Anmeldung>().HasIndex(a => a.NeueEmailTokenHash).IsUnique();
            builder.Entity<Anmeldung>()
                .HasOne(a => a.Veranstaltung)
                .WithMany(v => v.Anmeldungen)
                .HasForeignKey(a => a.VeranstaltungId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AnmeldungTag>().ToTable("AnmeldungTage");
            builder.Entity<AnmeldungTag>().HasKey(at => new { at.AnmeldungId, at.VeranstaltungsTagId });
            builder.Entity<AnmeldungTag>()
                .HasOne(at => at.Anmeldung)
                .WithMany(a => a.Tage)
                .HasForeignKey(at => at.AnmeldungId)
                .OnDelete(DeleteBehavior.Cascade);
            // Kein Cascade: SQL Server erlaubt keine zwei Löschpfade (Veranstaltung -> Anmeldung -> AnmeldungTag
            // und Veranstaltung -> Tag -> AnmeldungTag). Tage mit Anmeldungen werden ohnehin nur abgesagt, nicht gelöscht.
            builder.Entity<AnmeldungTag>()
                .HasOne(at => at.VeranstaltungsTag)
                .WithMany(t => t.AnmeldungTage)
                .HasForeignKey(at => at.VeranstaltungsTagId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnmeldungInfoEmail>().ToTable("AnmeldungInfoEmails");
            builder.Entity<AnmeldungInfoEmail>().HasIndex(i => new { i.AnmeldungId, i.Email }).IsUnique();
            builder.Entity<AnmeldungInfoEmail>().HasIndex(i => i.AbmeldeTokenHash).IsUnique();
            builder.Entity<AnmeldungInfoEmail>()
                .HasOne(i => i.Anmeldung)
                .WithMany(a => a.InfoEmails)
                .HasForeignKey(i => i.AnmeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AnmeldungEreignis>().ToTable("AnmeldungEreignisse");
            builder.Entity<AnmeldungEreignis>().HasIndex(e => new { e.AnmeldungId, e.ZeitpunktUtc });
            builder.Entity<AnmeldungEreignis>()
                .HasOne(e => e.Anmeldung)
                .WithMany(a => a.Ereignisse)
                .HasForeignKey(e => e.AnmeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BenachrichtigungEmpfaenger>().ToTable("BenachrichtigungEmpfaenger", t =>
                t.HasCheckConstraint(
                    "CK_BenachrichtigungEmpfaenger_UserIdOderEmail",
                    "(CASE WHEN [UserId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [Email] IS NULL THEN 0 ELSE 1 END) = 1"));
            // EF legt für nullbare Spalten gefilterte Indizes an (nur Zeilen mit Wert)
            builder.Entity<BenachrichtigungEmpfaenger>().HasIndex(b => new { b.VeranstaltungId, b.UserId }).IsUnique();
            builder.Entity<BenachrichtigungEmpfaenger>().HasIndex(b => new { b.VeranstaltungId, b.Email }).IsUnique();
            builder.Entity<BenachrichtigungEmpfaenger>().HasIndex(b => b.AbmeldeTokenHash).IsUnique();
            builder.Entity<BenachrichtigungEmpfaenger>()
                .HasOne(b => b.Veranstaltung)
                .WithMany(v => v.BenachrichtigungEmpfaenger)
                .HasForeignKey(b => b.VeranstaltungId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<BenachrichtigungEmpfaenger>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<VeranstaltungNachricht>().ToTable("VeranstaltungNachrichten");
            builder.Entity<VeranstaltungNachricht>().HasIndex(n => new { n.VeranstaltungId, n.GesendetUtc });
            builder.Entity<VeranstaltungNachricht>()
                .HasOne(n => n.Veranstaltung)
                .WithMany()
                .HasForeignKey(n => n.VeranstaltungId)
                .OnDelete(DeleteBehavior.Cascade);

            // Kalendereinträge, die zu einem Veranstaltungstag gehören (höchstens einer je Tag)
            builder.Entity<AppointmentData>().HasIndex(a => a.VeranstaltungsTagId).IsUnique();
            builder.Entity<AppointmentData>()
                .HasOne<VeranstaltungsTag>()
                .WithMany()
                .HasForeignKey(a => a.VeranstaltungsTagId)
                .OnDelete(DeleteBehavior.Cascade);
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
        public DbSet<AktuellesBeitrag> AktuellesBeitraege { get; set; }
        public DbSet<AktuellesBlock> AktuellesBloecke { get; set; }
        public DbSet<AktuellesBild> AktuellesBilder { get; set; }
        public DbSet<EmailAusgang> EmailAusgang { get; set; }
        public DbSet<Veranstaltung> Veranstaltungen { get; set; }
        public DbSet<VeranstaltungsTag> VeranstaltungsTage { get; set; }
        public DbSet<Anmeldung> Anmeldungen { get; set; }
        public DbSet<AnmeldungTag> AnmeldungTage { get; set; }
        public DbSet<AnmeldungInfoEmail> AnmeldungInfoEmails { get; set; }
        public DbSet<AnmeldungEreignis> AnmeldungEreignisse { get; set; }
        public DbSet<BenachrichtigungEmpfaenger> BenachrichtigungEmpfaenger { get; set; }
        public DbSet<VeranstaltungNachricht> VeranstaltungNachrichten { get; set; }
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
