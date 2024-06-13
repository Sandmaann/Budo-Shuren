
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace BudoShurenWebsite.Data
{
    public class NeuigkeitenAdaptor : DataAdaptor
    {
        private readonly UserService UserService;
        private readonly ApplicationDbContext DbContext;
        private readonly FileService FileService;
        private readonly IHostEnvironment Environment;


        public NeuigkeitenAdaptor(UserService userService, ApplicationDbContext dbContext, FileService fileService, IHostEnvironment environment)
        {
            UserService = userService;
            DbContext = dbContext;
            FileService = fileService;
            Environment = environment;
        }

        /// <summary>
        /// Returns the data collection after performing data operations based on request from <see cref=”DataManagerRequest”/>
        /// </summary>
        /// <param name="DataManagerRequest">DataManagerRequest contains the information regarding paging, grouping, filtering, searching, sorting which is handled on the Blazor DataGrid component side</param>
        /// <param name="Key">An optional parameter that can be used to perform additional data operations.</param>
        /// <returns>The data collection's type is determined by how this method has been implemented.</returns>
        public override async Task<object> ReadAsync(DataManagerRequest dm, string Key = null)
        {
            IEnumerable<Neuigkeit> DataSource = await DbContext.Neuigkeiten.ToListAsync();


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

            foreach (var item in DataSource)
            {
                item.EnvironmentPath = Environment.ContentRootPath;
            }
            return dm.RequiresCounts ? new DataResult() { Result = DataSource, Count = count } : (object)DataSource;
        }
        public override async Task<object> InsertAsync(DataManager dataManager, object record, string additionalParam)
        {
            //await Task.Delay(2500);

            if (record is Neuigkeit obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    if (obj.ID > 0)
                    {
                        var neuigkeit = await DbContext.Neuigkeiten.FindAsync(obj.ID);
                        if (neuigkeit != null)
                        {
                            neuigkeit.Titel = obj.Titel;
                            neuigkeit.Beschreibung = obj.Beschreibung;
                            neuigkeit.Sortierung = obj.Sortierung;
                            neuigkeit.Ort = obj.Ort;
                            neuigkeit.Link = obj.Link;
                            neuigkeit.Linktext = obj.Linktext;
                            neuigkeit.Datum = obj.Datum;
                            neuigkeit.LastChange = DateTime.Now;
                            neuigkeit.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";
                        }
                        else
                            throw new Exception("Neuigkeit mit ID " + obj.ID + " nicht gefunden!");
                    }
                    else
                    {
                        //ImagePath verarbeiten!
                        if (obj.TempFilePath == null)
                            throw new Exception("Kein Bild ausgewählt!");
                        if (!File.Exists(obj.TempFilePath))
                            throw new Exception("Datei " + obj.TempFilePath + " nicht gefunden!");

                        //Datei in Galerie-Ordner kopieren
                        string destination = FileService.GetGaleryDestinationPath(obj.TempFilePath);
                        await FileService.CopyFileAsync(obj.TempFilePath, destination);

                        if (File.Exists(destination))
                        {
                            //Temp-Datei löschen wenn sie noch vorhanden ist (sollte redundant sein?!)
                            if (File.Exists(obj.TempFilePath))
                                File.Delete(obj.TempFilePath);

                            obj.ImagePath = destination;

                            obj.Created = DateTime.Now;
                            obj.EntryCreatedBy = UserService.CurrentUser?.UserName ?? "unbekannt";
                            obj.LastChange = DateTime.Now;
                            obj.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";

                            await DbContext.Neuigkeiten.AddAsync(obj);

                            //Galerie-Eintrag erstellen

                            var galeryItem = new GalerieEintrag
                            {
                                Titel = obj.Titel,
                                Beschreibung = obj.Beschreibung,
                                ImagePath = obj.ImagePath,
                                ImageDate = obj.Datum,
                                EntryCreationDateUTC = obj.Created,
                                EntryCreatedBy = obj.EntryCreatedBy,
                                LastChangedUTC = obj.LastChange,
                                LastChangedBy = obj.LastChangedBy
                            };
                            await DbContext.Galerie.AddAsync(galeryItem);
                        }
                        else
                        {
                            throw new Exception("Fehler beim Kopieren der Datei " + obj.TempFilePath + " nach " + destination);
                        }
                    }
                }
                try
                {
                    await DbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception("Fehler beim Speichern der Neuigkeit: " + ex.Message);
                }
                //await DbContext.SaveChangesAsync();
            }
            else
                throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Neuigkeiten zu bearbeiten!");

            return record;
        }

        public override async Task<object> UpdateAsync(DataManager dm, object value, string keyField, string key)
        {
            if (value is Neuigkeit obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    var neuigkeit = await DbContext.Neuigkeiten.FindAsync(obj.ID);
                    if (neuigkeit != null)
                    {
                        neuigkeit.Titel = obj.Titel;
                        neuigkeit.Beschreibung = obj.Beschreibung;
                        neuigkeit.Sortierung = obj.Sortierung;
                        neuigkeit.Ort = obj.Ort;
                        neuigkeit.Link = obj.Link;
                        neuigkeit.Linktext = obj.Linktext;
                        neuigkeit.Datum = obj.Datum;
                        neuigkeit.LastChange = DateTime.Now;
                        neuigkeit.LastChangedBy = UserService.CurrentUser?.UserName ?? "unbekannt";

                        await DbContext.SaveChangesAsync();
                    }
                }
                else
                    throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Neuigkeiten zu bearbeiten!");
            }
            return value;
        }

        // Performs Remove operation
        public override async Task<object> RemoveAsync(DataManager dm, object value, string keyField, string key)
        {
            if (value is string obj)
            {
                await UserService.InitializeAsync();
                if (UserService.IsEditor || UserService.IsAbteilungsleiter || UserService.IsAdmin)
                {
                    var neuigkeit = await DbContext.Neuigkeiten.FindAsync(obj);
                    if (neuigkeit != null)
                    {
                        DbContext.Neuigkeiten.Remove(neuigkeit);
                        await DbContext.SaveChangesAsync();
                    }
                }
                else
                    throw new Exception(UserService.CurrentUser?.UserName + " hat nicht die Berechtigung Neuigkeiten zu bearbeiten!");
            }
            return value;
        }
    }
}