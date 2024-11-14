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
        public MitgliederAdaptor(UserService userService, UserManager<ApplicationUser> userManager, AuthenticationStateProvider authenticationStateProvider, ILogger<MitgliederAdaptor> logger)
        {
            UserService = userService;
            UserManager = userManager;
            AuthenticationStateProvider = authenticationStateProvider;
            Logger = logger;
        }

        private async Task<List<UserWithRoles>> GetUsersWithRoles()
        {
            try
            {
                var users = await UserManager.Users.ToListAsync();
                var usersWithRoles = new List<UserWithRoles>();

                var currentUser = await GetCurrentUser();
                if (currentUser != null)
                {
                    foreach (var user in users)
                    {
                        //User nach Rolle filtern. Admins und Abteilungsleiter dürfen alle sehen, alle anderen nur eigene Daten
                        //if (Global.Roles.IsAdmin(currentUser.Roles) || Global.Roles.IsAbteilungsleiter(currentUser.Roles))
                        //{



                        //    var roles = await UserManager.GetRolesAsync(user);
                        //    usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
                        //}

                        //Admin darf alle User sehen
                        if (Global.Roles.IsAdmin(currentUser.Roles))
                        {
                            var roles = await UserManager.GetRolesAsync(user);
                            usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
                        }
                        //Abteilungsleiter darf seine Abteilung sehen
                        else if (Global.Roles.IsAbteilungsleiter(currentUser.Roles))
                        {
                            if(user.Abteilung == currentUser.Abteilung)
                            {
                                var roles = await UserManager.GetRolesAsync(user);
                                usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
                            }
                        }
                        //Alle anderen dürfen nur sich selbst sehen
                        else if (user.UserName == currentUser.UserName)
                        {
                            var roles = await UserManager.GetRolesAsync(user);
                            usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                return usersWithRoles;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Benutzerdaten");
                throw;
            }
        }

        private async Task<UserWithRoles?> GetCurrentUser()
        {
            try
            {

                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var userPrincipal = authState.User;
                if (userPrincipal != null && userPrincipal?.Identity?.IsAuthenticated == true)
                {
                    var user = await UserManager.GetUserAsync(userPrincipal);
                    if (user != null)
                    {
                        var roles = await UserManager.GetRolesAsync(user);

                        return new UserWithRoles
                        {
                            User = user,
                            Roles = roles
                        };
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

        /// <summary>
        /// Returns the data collection after performing data operations based on request from <see cref=”DataManagerRequest”/>
        /// </summary>
        /// <param name="DataManagerRequest">DataManagerRequest contains the information regarding paging, grouping, filtering, searching, sorting which is handled on the Blazor DataGrid component side</param>
        /// <param name="Key">An optional parameter that can be used to perform additional data operations.</param>
        /// <returns>The data collection's type is determined by how this method has been implemented.</returns>
        public override async Task<object> ReadAsync(DataManagerRequest dm, string? Key = null)
        {
            try
            {
                IEnumerable<UserWithRoles> DataSource = await GetUsersWithRoles();

                int TotalRecordsCount = DataSource.Count();

                // Handling Searching in CustomAdaptor.
                if (dm.Search != null && dm.Search.Count > 0)
                {
                    // Searching
                    DataSource = DataOperations.PerformSearching(DataSource, dm.Search);
                    //Add custom logic here if needed and remove above method
                }
                if (dm.Sorted != null && dm.Sorted.Count > 0)
                {
                    // Sorting
                    DataSource = DataOperations.PerformSorting(DataSource, dm.Sorted);
                }

                int count = DataSource.Count();
                if (dm.Skip != 0)
                {
                    //Paging
                    DataSource = DataOperations.PerformSkip(DataSource, dm.Skip);
                }
                if (dm.Take != 0)
                {
                    DataSource = DataOperations.PerformTake(DataSource, dm.Take);
                }
                return dm.RequiresCounts ? new DataResult() { Result = DataSource, Count = count } : (object)DataSource;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Mitgliederdaten");
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