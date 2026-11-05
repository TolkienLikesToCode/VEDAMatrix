using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace VedaMatrix;

public partial class App : Application
{
    public override void Initialize()
    {
        Console.WriteLine("[App] Initialize...");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[App] OnFrameworkInitializationCompleted...");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                desktop.MainWindow = new MainWindow();
                Console.WriteLine("[App] MainWindow erstellt.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[App] FEHLER beim Erstellen des MainWindow:");
                Console.WriteLine(ex);
                Console.ResetColor();
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
