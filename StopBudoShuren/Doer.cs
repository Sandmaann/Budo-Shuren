using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace StopBudoShuren
{
    internal static class Doer
    {
        public static string Url { get; set; } = @"https://localhost:7280/Custom/Api/StopApp";
        public static async Task StopApp()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Korrigierte URL, die dem Routenmuster des ApiController entspricht
                    //string url = "https://localhost:7280/Custom/Api/StopApp";
                    HttpResponseMessage response = await httpClient.GetAsync(Url);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Anwendung erfolgreich gestoppt.");
                    }
                    else
                    {
                        Console.WriteLine($"Fehler beim Abrufen der Daten: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ein Fehler ist aufgetreten: {ex.Message}");
                }
            }
        }
    }
}
