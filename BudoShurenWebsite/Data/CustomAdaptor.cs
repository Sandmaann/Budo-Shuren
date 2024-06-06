using Microsoft.AspNetCore.Identity;
using Syncfusion.Blazor.Data;
using Syncfusion.Blazor;
using Microsoft.EntityFrameworkCore;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace BudoShurenWebsite.Data
{
    /// <summary>
    /// Implementing CustomAdaptor by extending the <see cref=“DataAdaptor”/> class.
    /// The Blazor DataGrid component support for custom data binding, which enables the binding and manipulation of data in a personalized way, using user-defined methods.
    /// </summary>
    public class CustomAdaptor : DataAdaptor
    {
        public UserManager<ApplicationUser> UserManager { get; set; }
        public AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        public CustomAdaptor(UserManager<ApplicationUser> userManager, AuthenticationStateProvider authenticationStateProvider)
        {
            UserManager = userManager;
            AuthenticationStateProvider = authenticationStateProvider;
        }


        private async Task<List<UserWithRoles>> GetUsersWithRoles()
        {
            var users = await UserManager.Users.ToListAsync();
            var usersWithRoles = new List<UserWithRoles>();

            foreach (var user in users)
            {
                var roles = await UserManager.GetRolesAsync(user);
                usersWithRoles.Add(new UserWithRoles { User = user, Roles = roles });
            }

            return usersWithRoles;
        }

        private async Task<UserWithRoles?> GetCurrentUser()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var userPrincipal = authState.User;
            if (userPrincipal != null && userPrincipal?.Identity?.IsAuthenticated == true)
            {
                var user = await UserManager.GetUserAsync(userPrincipal);
                var roles = await UserManager.GetRolesAsync(user);

                return new UserWithRoles
                {
                    User = user,
                    Roles = roles
                };
            }
            return null;
        }

        /// <summary>
        /// Returns the data collection after performing data operations based on request from <see cref=”DataManagerRequest”/>
        /// </summary>
        /// <param name="DataManagerRequest">DataManagerRequest contains the information regarding paging, grouping, filtering, searching, sorting which is handled on the Blazor DataGrid component side</param>
        /// <param name="Key">An optional parameter that can be used to perform additional data operations.</param>
        /// <returns>The data collection's type is determined by how this method has been implemented.</returns>
        public override async Task<object> ReadAsync(DataManagerRequest dm, string Key = null)
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

        //    //Here RequiresCount is passed from the control side itself, where ever the on-demand data fetching is needed then the RequiresCount is set as true in component side itself.
        //    return dm.RequiresCounts ? new DataResult() { Result = DataSource, Count = TotalRecordsCount } : (object)DataSource;
        //}

        // Performs Update operation
        public override async Task<object> UpdateAsync(DataManager dm, object value, string keyField, string key)
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


            //var data = Orders.Where(or => or.OrderID == (value as Order).OrderID).FirstOrDefault();
            //if (user != null && value is ApplicationUser valueUser)
            //{
            //    user.Vorname = valueUser.Vorname;
            //    user.Name = valueUser.Name;
            //    user.Abteilung = valueUser.Abteilung;

            //    await UserManager.UpdateAsync(user);
            //}
            //return value;
        }

        // Performs Remove operation
        public override async Task<object> RemoveAsync(DataManager dm, object value, string keyField, string key)
        {
            var user = await UserManager.FindByNameAsync((value as ApplicationUser)?.UserName);
            if (user != null)
            {
                await UserManager.RemoveLoginAsync(user, user.Id, user.Email);
            }
            return value;
        }
    }
}
