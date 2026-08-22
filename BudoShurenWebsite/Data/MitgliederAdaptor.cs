using Microsoft.AspNetCore.Identity;
using Syncfusion.Blazor.Data;
using Syncfusion.Blazor;
using Microsoft.EntityFrameworkCore;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Components.Authorization;
using BudoShurenWebsite.Services;

namespace BudoShurenWebsite.Data
{
    /// <summary>
    /// Implementing CustomAdaptor by extending the <see cref=“DataAdaptor”/> class.
    /// The Blazor DataGrid component support for custom data binding, which enables the binding and manipulation of data in a personalized way, using user-defined methods.
    /// </summary>
    public class MitgliederAdaptor : DataAdaptor
    {
        public UserManager<ApplicationUser> UserManager { get; set; }
        public AuthenticationStateProvider AuthenticationStateProvider { get; set; }
        public ILogger<MitgliederAdaptor> Logger { get; set; }
        private readonly UserService UserService;
        private readonly IDbContextFactory<ApplicationDbContext> DbContextFactory;


        public MitgliederAdaptor(
            UserService userService,
            UserManager<ApplicationUser> userManager,
            AuthenticationStateProvider authenticationStateProvider,
            ILogger<MitgliederAdaptor> logger,
            IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            UserService = userService;
            UserManager = userManager;
            AuthenticationStateProvider = authenticationStateProvider;
            Logger = logger;
            DbContextFactory = dbContextFactory;
        }
        private async Task<List<UserWithRoles>> GetUsersWithRoles()
        {
            try
            {
                using var context = DbContextFactory.CreateDbContext();

                // Lade alle Benutzer
                var users = await context.Users.ToListAsync();
                var usersWithRoles = new List<UserWithRoles>();
                var currentUser = await GetCurrentUser();

                if (currentUser != null)
                {
                    foreach (var user in users)
                    {
                        if (user.Verified == false)
                            continue;

                        // Nutze den lokalen Context um Rollen zu laden
                        var roles = await context.UserRoles
                            .Where(ur => ur.UserId == user.Id)
                            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                            .ToListAsync();

                        // Filter-Logik
                        bool shouldAdd = false;

                        if (Global.Roles.IsAdmin(currentUser.Roles))
                        {
                            shouldAdd = true;
                        }
                        else if (Global.Roles.IsAbteilungsleiter(currentUser.Roles))
                        {
                            shouldAdd = user.Abteilung == currentUser.Abteilung;
                        }
                        else if (user.UserName == currentUser.UserName)
                        {
                            shouldAdd = true;
                        }

                        if (shouldAdd)
                        {
                            usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
                        }
                    }
                }
                return usersWithRoles;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Benutzerdaten");
                if (!ex.Message.StartsWith("A second operation was started on this context instance before a previous operation completed."))
                {
                    throw;
                }
                else
                {
                    //ignore
                    throw new Exception("Wenn keine Daten angezeigt werden, bitte die Seite aktualisieren.");
                }
            }
        }

        private async Task<UserWithRoles?> GetCurrentUser()
        {
            try
            {
                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var userPrincipal = authState.User;

                if (userPrincipal?.Identity?.IsAuthenticated == true)
                {
                    var user = await UserManager.GetUserAsync(userPrincipal);
                    if (user != null)
                    {
                        var roles = await UserManager.GetRolesAsync(user);
                        return new UserWithRoles { User = user, Roles = roles };
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden des aktuellen Benutzers");
                throw;
            }
        }

        public override async Task<object> ReadAsync(DataManagerRequest dm, string? Key = null)
        {
            try
            {
                IEnumerable<UserWithRoles> DataSource = await GetUsersWithRoles();

                int TotalRecordsCount = DataSource.Count();

                if (dm.Search != null && dm.Search.Count > 0)
                {
                    DataSource = DataOperations.PerformSearching(DataSource, dm.Search);
                }
                if (dm.Sorted != null && dm.Sorted.Count > 0)
                {
                    DataSource = DataOperations.PerformSorting(DataSource, dm.Sorted);
                }

                int count = DataSource.Count();
                if (dm.Skip != 0)
                {
                    DataSource = DataOperations.PerformSkip(DataSource, dm.Skip);
                }
                if (dm.Take != 0)
                {
                    DataSource = DataOperations.PerformTake(DataSource, dm.Take);
                }
                return dm.RequiresCounts ? new DataResult { Result = DataSource, Count = count } : new DataResult { Result = DataSource };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Lesen der Daten");
                throw;
            }
        }

        // Performs Update operation
        public override async Task<object> UpdateAsync(DataManager dm, object value, string keyField, string key)
        {
            try
            {
                if (value is UserWithRoles obj)
                {
                    var user = await UserManager.FindByNameAsync(obj.UserName);
                    if (user != null)
                    {
                        user.Vorname = obj.Vorname;
                        user.Name = obj.Name;
                        user.Abteilung = obj.Abteilung;

                        if (obj.User.Verified && !user.Verified)
                        {
                            var currentUser = await GetCurrentUser();
                            if (currentUser != null)
                            {
                                if (Global.Roles.IsAdmin(currentUser.Roles) || Global.Roles.IsAbteilungsleiter(currentUser.Roles))
                                {
                                    user.Verified = obj.User.Verified;
                                    user.VerifiedAt = DateTime.Now;
                                    user.VerifiedBy = currentUser?.User?.UserName ?? "unbekannt";
                                }
                                else
                                    throw new Exception(currentUser?.User?.UserName + " hat nicht die Berechtigung Benutzer zu aktivieren!");
                            }
                        }


                        await UserManager.UpdateAsync(user);

                        //Rolle aktualisieren wenn geändert

                        var roles = await UserManager.GetRolesAsync(user);
                        if (roles != obj.Roles)
                        {
                            await UserManager.RemoveFromRolesAsync(user, roles);
                            await UserManager.AddToRolesAsync(user, obj.Roles);
                        }
                    }
                }
                return value;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aktualisieren der Mitgliederdaten");
                throw;
            }
        }

        // Performs Remove operation
        public override async Task<object> RemoveAsync(DataManager dm, object value, string keyField, string key)
        {
            try
            {
                if (value is string email)
                {
                    var user = await UserManager.FindByNameAsync(email);
                    if (user != null && user.Email != null)
                    {
                        await UserService.InitializeAsync();
                        var currentUser = UserService.CurrentUser;
                        if (user.Id == currentUser.User.Id)
                            throw new InvalidOperationException("Bitte lösche dich nicht selbst!");

                        var result = await UserManager.RemoveLoginAsync(user, user.Id, user.Email);
                        if (result?.Succeeded == true)
                        {
                            result = await UserManager.DeleteAsync(user);
                        }
                    }
                }
                return value;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Löschen der Mitgliederdaten");
                throw;
            }
        }
    }
}