using BudoShurenWebsite.Components;
using BudoShurenWebsite.Components.Account;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Data;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using Syncfusion.Blazor;
using Microsoft.Extensions.Hosting;
using System;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Globalization;

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BudoShurenWebsite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddSyncfusionBlazor();
            // Register the locale service to localize the  SyncfusionBlazor components.
            builder.Services.AddSingleton(typeof(ISyncfusionStringLocalizer), typeof(SyncfusionLocalizer));

            builder.Services.AddControllers();
            builder.Services.AddHttpClient();


            //DEAKTIVEREN !!!
            builder.Services.AddServerSideBlazor(options => options.DetailedErrors = true);
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddSingleton<IDataService, DataService>();
            builder.Services.AddSingleton<IImageUploadService, ImageUploadService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
                .AddIdentityCookies();

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("***ENTFERNT***");

            var connectionString = string.Empty;
            if (builder.Environment.IsDevelopment())
            {
                connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                //connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }
            else
            {
                connectionString = Environment.GetEnvironmentVariable("DefaultConnection");
            }
            if(string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            //var connection = String.Empty;
            //if (builder.Environment.IsDevelopment())
            //{
            //    //builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
            //    connection = builder.Configuration.GetConnectionString("DefaultConnection");
            //}
            //else
            //{
            //    connection = Environment.GetEnvironmentVariable("DefaultConnection");
            //}
            //builder.Services.AddDbContext<ApplicationDbContext>(options =>
            //    options.UseSqlServer(connection));
            //builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("de-DE");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("de-DE");

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            //Nur für Release oder bei Migration wichtig
            //MigrateDatabase(app);

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();
            app.MapControllers();




            app.Run();
        }

        private static void MigrateDatabase(WebApplication host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<ApplicationDbContext>();

            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Console.WriteLine("Datenbankmigration gestartet");
                dbContext.Database.Migrate();
                watch.Stop();
                Console.WriteLine($"Datenbankmigration erfolgreich durchgeführt: {watch.ElapsedMilliseconds} ms elapsed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Ausführen der Datenbankmigration: " + ex.Message);
            }
        }
    }
}
