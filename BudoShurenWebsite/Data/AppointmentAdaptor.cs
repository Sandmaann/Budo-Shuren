using Microsoft.AspNetCore.Identity;
using Syncfusion.Blazor.Data;
using Syncfusion.Blazor;
using Microsoft.EntityFrameworkCore;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Components.Authorization;
using BudoShurenWebsite.Services;
using Microsoft.AspNetCore.Components;

namespace BudoShurenWebsite.Data
{

    public class AppointmentAdaptor : DataAdaptor
    {
        private readonly UserService UserService;
        private readonly ApplicationDbContext DbContext;
        private readonly IHostEnvironment Environment;

        [Parameter]
        public string? Abteilung { get; set; }

        [Parameter]
        public bool IsWeek { get; set; }

        public AppointmentAdaptor(UserService userService, ApplicationDbContext dbContext, IHostEnvironment environment)
        {
            UserService = userService;
            DbContext = dbContext;
            Environment = environment;
        }

        /// <summary>
        /// Returns the data collection after performing data operations based on request from <see cref=”DataManagerRequest”/>
        /// </summary>
        /// <param name="DataManagerRequest">DataManagerRequest contains the information regarding paging, grouping, filtering, searching, sorting which is handled on the Blazor DataGrid component side</param>
        /// <param name="Key">An optional parameter that can be used to perform additional data operations.</param>
        /// <returns>The data collection's type is determined by how this method has been implemented.</returns>
        public override async Task<object> ReadAsync(DataManagerRequest dm, string? Key = null)
        {
            //if (dm.Table == null)
            //    return new List<AppointmentData>();

            IEnumerable<AppointmentData> DataSource = await DbContext.Appointments.ToListAsync();

            if (dm.Table != null)
            {
                if(dm.Table != "EventDatas")
                { }
                switch (dm.Table)
                {
                    case "Week":
                        DataSource = DataSource.Where(x => x.ShowInWeek);
                        break;
                    case "Month":
                        DataSource = DataSource.Where(x => x.ShowInMonth);
                        break;

                }
            }

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
            if (dm.Where != null && dm.Where.Count > 0)
            {
                DataSource = DataOperations.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
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
        public override async Task<object> InsertAsync(DataManager dataManager, object record, string? additionalParam)
        {
            //await Task.Delay(2500);

            if (record is AppointmentData obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    if (obj.Id > 0)
                    {
                        //Recurrence wurde geändert - die ID ist zwar da, aber ich muss einen neuen Eintrag anlegen
                        //(der originale mit Recurrence wurde geändert sodass der Termin ausgenommen wird,
                        //und ein neuer Eintrag für den geänderten Termin angelegt wird)
                        var appointment = new AppointmentData();
                        appointment.CopyPropertiesFrom(obj);
                        appointment.Id = 0;
                        appointment.Created = DateTime.Now;
                        appointment.EntryCreatedBy = UserService.CurrentUser?.UserName ?? "unbekannt";
                        appointment.LastChange = DateTime.Now;
                        appointment.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";
                        await DbContext.Appointments.AddAsync(appointment);
                    }
                    else
                    {
                        obj.Created = DateTime.Now;
                        obj.EntryCreatedBy = UserService.CurrentUser?.UserName ?? "unbekannt";
                        obj.LastChange = DateTime.Now;
                        obj.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";

                        await DbContext.Appointments.AddAsync(obj);
                    }
                }
                try
                {
                    await DbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception("Fehler beim Speichern des Kalendereintrags: " + ex.Message);
                }
            }
            else
                throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Kalendereinträge zu bearbeiten!");

            return record;
        }

        public override async Task<object> UpdateAsync(DataManager dm, object value, string keyField, string key)
        {
            if (value is AppointmentData obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    var appointment = await DbContext.Appointments.FindAsync(obj.Id);
                    if (appointment != null)
                    {
                        appointment.CopyPropertiesFrom(obj);

                        appointment.LastChange = DateTime.Now;
                        appointment.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";

                        await DbContext.SaveChangesAsync();
                    }
                }
                else
                    throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Kalendereinträge zu bearbeiten!");
            }
            return value;
        }

        // Performs Remove operation
        public override async Task<object> RemoveAsync(DataManager dm, object value, string keyField, string key)
        {
            if (value is int obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    var appointment = await DbContext.Appointments.FindAsync(obj);
                    if (appointment != null)
                    {
                        DbContext.Appointments.Remove(appointment);
                        await DbContext.SaveChangesAsync();
                    }
                }
                else
                    throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Kalendereinträge zu bearbeiten!");
            }
            return value;
        }
        public override async Task<object> BatchUpdateAsync(DataManager dataManager, object changedRecords, object addedRecords, object deletedRecords, string primaryColumnName, string key, int? dropIndex)
        {
            if (changedRecords != null && changedRecords is IEnumerable<object> changedList)
            {
                foreach (var changed in changedList)
                {
                    if (changed is AppointmentData changedData)
                    {
                        try
                        {
                            await UpdateAsync(dataManager, changedData, primaryColumnName, key);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
            if (addedRecords != null && addedRecords is IEnumerable<object> addedList)
            {
                foreach (var added in addedList)
                {
                    if (added is AppointmentData addedData)
                    {
                        await InsertAsync(dataManager, addedData, null);
                    }
                }
            }
            if (deletedRecords != null && deletedRecords is IEnumerable<object> deletedList)
            {
                foreach (var deleted in deletedList)
                {
                    if (deleted is int deletedData)
                    {
                        await RemoveAsync(dataManager, deletedData, primaryColumnName, key);
                    }
                }
            }
            return base.BatchUpdateAsync(dataManager, changedRecords, addedRecords, deletedRecords, primaryColumnName, key, dropIndex);
        }
   
    }
}
