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
using BudoShurenWebsite.Global;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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

            //builder.Services.AddSingleton<OrderDataAccessLayer>();
            builder.Services.AddScoped<CustomAdaptor>();
            //builder.Services.AddScoped<ServiceClass>();

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
            }
            else
            {
                connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

                //TODO das umsetzen!
                //connectionString = Environment.GetEnvironmentVariable("DefaultConnection");
            }
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            //builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            //    .AddEntityFrameworkStores<ApplicationDbContext>()
            //    .AddSignInManager()
            //    .AddDefaultTokenProviders()
            //    .AddRoles<IdentityRole>();

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("NotGuest", policy => policy.RequireAssertion(context =>
                    !context.User.IsInRole(Roles.Gast) && context.User.Claims.Any(c => c.Type == ClaimTypes.Role)));
            });

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

            try
            {
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                InitializeRoles(roleManager, userManager).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Initialisieren der Rollen: " + ex.Message);
            }
        }

        private static async Task InitializeRoles(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            foreach (var role in Global.Roles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }

    public class AtLeastOneRoleRequirement : IAuthorizationRequirement { }
    public class AtLeastOneRoleHandler : AuthorizationHandler<AtLeastOneRoleRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AtLeastOneRoleRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated == true && context.User.Claims.Any(c => c.Type == ClaimTypes.Role))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}
