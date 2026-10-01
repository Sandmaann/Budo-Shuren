using BudoShurenWebsite.Components;
using BudoShurenWebsite.Components.Account;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Middleware;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Syncfusion.Blazor;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

namespace BudoShurenWebsite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            // In der Umgebung "Test" (Tests, "dotnet ef") nur Warnungen auf die Konsole, nie an BetterStack.
            // Bewusst ohne Datei: fehlt eine Konfigurationsdatei, lädt NLog sonst still die nlog.config.
            var logger = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Test"
                ? NLog.LogManager.Setup()
                    .LoadConfiguration(c => c.ForLogger().FilterMinLevel(NLog.LogLevel.Warn).WriteToConsole())
                    .GetCurrentClassLogger()
                : NLog.LogManager.Setup()
                    .LoadConfigurationFromFile("nlog.config")
                    .GetCurrentClassLogger();


            //var logger = NLog.LogManager.Setup()
            //.LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            try
            {
                // Registrieren des Ereignishandlers für unbeobachtete Task-Ausnahmen
                TaskScheduler.UnobservedTaskException += (sender, e) =>
                {
                    logger.Log(NLog.LogLevel.Error,
                        e.Exception,
                        "Ein unbehandelter Fehler in einem Task wurde festgestellt.");
                    e.SetObserved(); // Verhindert den Prozessabbruch
                };
                logger.Log(NLog.LogLevel.Info, "Starting BudoShuren");

                var builder = WebApplication.CreateBuilder(args);

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                builder.Services.AddSyncfusionBlazor();
                // Register the locale service to localize the  SyncfusionBlazor components.
                builder.Services.AddSingleton(typeof(ISyncfusionStringLocalizer), typeof(SyncfusionLocalizer));

                // Konfiguriere die Datenprotektion, um Schlüssel im Dateisystem zu speichern
                //builder.Services.AddDataProtection()
                //    .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
                //    .SetDefaultKeyLifetime(TimeSpan.FromDays(90)) // Schlüssel alle 90 Tage rotieren
                //    .SetApplicationName("BudoShurenWebsite");

                builder.Services.AddControllers();
                builder.Services.AddHttpClient();
                builder.Services.AddMemoryCache();

                //Authentifizierung
                builder.Services.AddCascadingAuthenticationState();
                builder.Services.AddScoped<IdentityUserAccessor>();
                builder.Services.AddScoped<IdentityRedirectManager>();
                builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
                builder.Services.AddScoped<IAuthorizationHandler, VerifiedUserHandler>();

                //Meine Dienste
                //builder.Services.AddScoped<GooglereCAPTCHAv3Service>();
                builder.Services.AddScoped<SessionService>();
                builder.Services.AddScoped<UserService>();
                builder.Services.AddScoped<EmailSender>();
                builder.Services.AddScoped<ImageService>();
                //builder.Services.AddSingleton<IImageUploadService, ImageUploadService>();
                builder.Services.AddScoped<VisitorCounterService>();
                builder.Services.AddScoped<WissenService>();
                builder.Services.AddScoped<SlugService>();
                builder.Services.AddScoped<IAktuellesService, AktuellesService>();

                //Mailversand: SMTP-Transport, Warteschlange und Hintergrundversand (siehe Services/Mail)
                builder.Services.AddSingleton(TimeProvider.System);
                builder.Services.AddOptions<EmailVersandOptionen>()
                    .Bind(builder.Configuration.GetSection(EmailVersandOptionen.Abschnitt))
                    .Validate(o => o.IstGueltig(), $"Ungültige Einstellungen im Abschnitt {EmailVersandOptionen.Abschnitt}")
                    .ValidateOnStart();
                builder.Services.AddSingleton<IMailTransport, MailKitTransport>();
                builder.Services.AddSingleton<EmailVersandSignal>();
                builder.Services.AddSingleton<IEmailWarteschlange, EmailWarteschlange>();
                builder.Services.AddSingleton<EmailVersandJob>();
                builder.Services.AddHostedService<EmailVersandHostedService>();

                //Adapter für SfGrid & SfScheduler
                builder.Services.AddScoped<MitgliederAdaptor>();
                builder.Services.AddScoped<NeueMitgliederAdaptor>();
                builder.Services.AddScoped<NeuigkeitenAdaptor>();
                builder.Services.AddScoped<AppointmentAdaptor>();
                builder.Services.AddScoped<WissenBeitragAdaptor>();
                builder.Services.AddScoped<AktuellesBeitragAdaptor>();

                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                }).AddIdentityCookies();

                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("***ENTFERNT***");

                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                }

                builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString));
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString), optionsLifetime: ServiceLifetime.Singleton);

                builder.Services.AddDbContext<DataProtectionKeyContext>(options =>
                    options.UseSqlServer(connectionString));

                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                // Konfiguriere die Datenverschlüsselung, um EF Core zu verwenden
                builder.Services.AddDataProtection()
                    .PersistKeysToDbContext<DataProtectionKeyContext>();

                builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddSignInManager()
                .AddDefaultTokenProviders();

                builder.Services.AddScoped<AdminMaintenanceService>();

                builder.Services.AddAuthorization(options =>
                {
                    options.AddPolicy("Aktiviert", policy => policy.Requirements.Add(new VerifiedUserRequirement()));
                    options.AddPolicy("NotGuest", policy => policy.RequireAssertion(context =>
                        !context.User.IsInRole(Roles.Gast) && context.User.Claims.Any(c => c.Type == ClaimTypes.Role)));
                    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
                });

                builder.Services.AddSingleton<IEmailSender<ApplicationUser>, EmailSender>();

                // Registrieren Sie den MemoryCache-Dienst
                builder.Services.AddMemoryCache();
                builder.Services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(15); // Setze die Timeout-Dauer
                    options.Cookie.HttpOnly = true; // Setze HttpOnly auf true
                    options.Cookie.IsEssential = true; // Setze IsEssential auf true
                });
                builder.Services.AddHttpContextAccessor();

                CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("de-DE");
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("de-DE");

                // NLog Logging provider
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                // Debug Sachen - zu laut für prod
                //builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                //builder.Logging.AddConsole();
                //builder.Logging.AddDebug();
                //builder.Logging.AddEventLog();

                builder.Services.AddDistributedMemoryCache();

                var app = builder.Build();

                // GANZ ZUERST: Wartungsmodus-Check, bevor irgendetwas anderes läuft
                app.UseMiddleware<MaintenanceMiddleware>();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                    app.UseMigrationsEndPoint();
                }
                else
                {
                    //Fehlerseite anzeigen
                    app.UseExceptionHandler(errorApp =>
                    {
                        errorApp.Run(async context =>
                        {
                            context.Response.StatusCode = 500; // Interner Serverfehler
                            context.Response.ContentType = "text/html";

                            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                            if (exceptionHandlerPathFeature?.Error != null)
                            {
                                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                                logger.LogError(
                                    exceptionHandlerPathFeature.Error,
                                    "Unbehandelter Fehler bei {Method} {Path}. RequestId: {RequestId}",
                                    context.Request.Method,
                                    context.Request.Path,
                                    context.TraceIdentifier);

                                //var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                                //logger.LogError(exceptionHandlerPathFeature.Error, "Unbehandelter Fehler");
                            }

                            // Umleitung zur Fehlerseite
                            context.Response.Redirect("/Error");
                        });
                    });

                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }


                //Nur bei DB-Migration wichtig
                MigrateDatabase(app, logger);

                app.UseHttpsRedirection();
                app.UseStaticFiles();
                app.UseAntiforgery();
                app.UseSession();

                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

                // Add additional endpoints required by the Identity /Account Razor components.
                app.MapAdditionalIdentityEndpoints();
                app.MapControllers();
                app.Run();

                stopwatch.Stop();
                logger.Log(NLog.LogLevel.Info, "BudoShuren startet in {Milliseconds} ms", stopwatch.ElapsedMilliseconds);
            }
            catch (HostAbortedException)
            {
                // Normal bei "dotnet ef": die Tools brechen den Start nach dem Aufbau des Hosts ab, kein Fehler
                throw;
            }
            catch (Exception exception)
            {
                // NLog: catch setup errors
                logger.Error(exception, "An error occurred during application startup");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (to avoid segmentation faults on Linux)
                NLog.LogManager.Shutdown();
            }
        }

        private static void MigrateDatabase(WebApplication host, Logger logger)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<ApplicationDbContext>();

            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                logger.Info("Datenbankmigration gestartet");
                Console.WriteLine("Datenbankmigration gestartet");
                dbContext.Database.Migrate();
                watch.Stop();

                logger.Log(NLog.LogLevel.Info, $"Datenbankmigration erfolgreich durchgeführt: {watch.ElapsedMilliseconds} ms elapsed");
                Console.WriteLine($"Datenbankmigration erfolgreich durchgeführt: {watch.ElapsedMilliseconds} ms elapsed");
            }
            catch (Exception ex)
            {
                logger.Log(NLog.LogLevel.Error, ex, "Fehler beim Ausführen der Datenbankmigration");
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
                logger.Log(NLog.LogLevel.Error, ex, "Fehler beim Initialisieren der Rollen");
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

    public class VerifiedUserRequirement : IAuthorizationRequirement { }
    public class VerifiedUserHandler : AuthorizationHandler<VerifiedUserRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public VerifiedUserHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, VerifiedUserRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(context.User);
                if (user != null && user.Verified)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}
