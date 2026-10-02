using System.Net;
using System.Net.Http.Headers;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>Upload über den FilesaveController: nur Editoren mit vorlaeufig=true (Aktuelles) markieren ihre Bilder.</summary>
[Trait("Category", "Integration")]
public class BildUploadTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private static async Task<HttpClient> AdminClientAsync(TestWebAppFactory app)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "admin", Email = "admin@example.org", Vorname = "Admin", Verified = true };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, Roles.Admin)).Succeeded.ShouldBeTrue();

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAnmeldung.Header, user.Id);
        return client;
    }

    private static MultipartFormDataContent Png(string dateiname)
    {
        using var bild = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(40, 30);
        var daten = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(bild, daten);
        var datei = new ByteArrayContent(daten.ToArray());
        datei.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        // Der Feldname muss die ID des SfUploaders sein (siehe FilesaveController.Save)
        return new MultipartFormDataContent { { datei, "UploadFiles", dateiname } };
    }

    [DatenbankFact]
    public async Task Nur_Uploads_mit_vorlaeufig_werden_markiert()
    {
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = await AdminClientAsync(app);

        (await client.PostAsync("/Account/Member/Filesave/Save?vorlaeufig=true", Png("aktuelles.png"), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync("/Account/Member/Filesave/Save", Png("galerie.png"), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var kontext = Datenbank.NeuerKontext();
        var bilder = await kontext.Images.ToDictionaryAsync(i => i.Title, i => i.VorlaeufigSeitUtc != null, Abbruch);
        bilder.ShouldBe(new Dictionary<string, bool> { ["aktuelles.png"] = true, ["galerie.png"] = false });
    }
}
