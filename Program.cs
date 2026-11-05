using System;
using Avalonia;

namespace VedaMatrix;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("VEDA Matrix – Starte Anwendung...");
            Console.WriteLine($"BaseDirectory : {AppContext.BaseDirectory}");
            Console.WriteLine($"CurrentDir    : {Directory.GetCurrentDirectory()}");
            Console.WriteLine();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("========================================");
            Console.WriteLine("FATALER FEHLER beim Start:");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Drücke Enter zum Beenden...");
            Console.ReadLine();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
