using System.IO;

namespace BudoShurenWebsite.Middleware
{
    public class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _flagPath;

        public MaintenanceMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            // Muss mit dem Pfad im Deploy-Skript übereinstimmen: $TargetPath\maintenance.flag
            _flagPath = Path.Combine(env.ContentRootPath, "maintenance.flag");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (File.Exists(_flagPath))
            {   
                context.Response.StatusCode = 503;
                context.Response.Headers["Retry-After"] = "60";
                context.Response.ContentType = "text/html; charset=utf-8";

                await context.Response.WriteAsync(@"
<!DOCTYPE html>
<html lang='de'>
<head>
    <meta charset='utf-8' />
    <title>Wartungsarbeiten</title>
    <style>
        body { font-family: sans-serif; text-align: center; margin-top: 10%; background:#f5f5f5; color:#333; }
        h1 { font-size: 2em; }
        p { font-size: 1.1em; }
    </style>
</head>
<body>
    <h1>🔧 Kurze Wartungspause</h1>
    <p>Moin, ich bin gerade im Update &ndash; bin gleich wieder da!</p>
</body>
</html>");
                return; // WICHTIG: kein Aufruf von _next(), sonst läuft der Request trotzdem weiter
            }

            await _next(context);
        }
    }
}