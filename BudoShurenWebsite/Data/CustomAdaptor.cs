using Microsoft.AspNetCore.Identity;
using Syncfusion.Blazor.Data;
using Syncfusion.Blazor;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Data
{
    /// <summary>
    /// Implementing CustomAdaptor by extending the <see cref=“DataAdaptor”/> class.
    /// The Blazor DataGrid component support for custom data binding, which enables the binding and manipulation of data in a personalized way, using user-defined methods.
    /// </summary>
    public class CustomAdaptor : DataAdaptor
    {
        public UserManager<ApplicationUser> UserManager { get; set; }
        public CustomAdaptor(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        /// <summary>
        /// Returns the data collection after performing data operations based on request from <see cref=”DataManagerRequest”/>
        /// </summary>
        /// <param name="DataManagerRequest">DataManagerRequest contains the information regarding paging, grouping, filtering, searching, sorting which is handled on the Blazor DataGrid component side</param>
        /// <param name="Key">An optional parameter that can be used to perform additional data operations.</param>
        /// <returns>The data collection's type is determined by how this method has been implemented.</returns>
        public override async Task<object> ReadAsync(DataManagerRequest dm, string Key = null)
        {
            IEnumerable<ApplicationUser> DataSource = await UserManager.Users.ToListAsync();
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
            var user = await UserManager.FindByNameAsync((value as ApplicationUser)?.UserName);
            //var data = Orders.Where(or => or.OrderID == (value as Order).OrderID).FirstOrDefault();
            if (user != null && value is ApplicationUser valueUser)
            {
                user.Vorname = valueUser.Vorname;
                user.Name = valueUser.Name;
                user.Abteilung = valueUser.Abteilung;

                await UserManager.UpdateAsync(user);
            }
            return value;
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
