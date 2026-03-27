using Avalonia;
using Avalonia.Native;
using System;
using System.IO;

namespace ClinicVetsSystem;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Log file on Desktop for crash diagnosis
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "clinicvets_crash.log");

        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now}] App starting...\n");

            // Initialize Supabase before the UI starts
            File.AppendAllText(logPath, $"[{DateTime.Now}] Initializing Supabase...\n");
            SupabaseService.InitializeAsync().GetAwaiter().GetResult();
            File.AppendAllText(logPath, $"[{DateTime.Now}] Supabase OK. Starting UI...\n");

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            File.AppendAllText(logPath, $"[{DateTime.Now}] App exited normally.\n");
        }
        catch (Exception ex)
        {
            var msg = $"[{DateTime.Now}] CRASH:\n{ex}\n\n";
            File.AppendAllText(logPath, msg);
            // Re-throw so macOS still gets the crash report
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new AvaloniaNativePlatformOptions
            {
                // Software rendering — פותר בעיות GPU/Metal על macOS 26
                RenderingMode = new[] { AvaloniaNativeRenderingMode.Software }
            })
            .WithInterFont()
            .LogToTrace();
}