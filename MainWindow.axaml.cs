using System;
using Avalonia.Controls;

namespace VedaMatrix;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            Console.WriteLine("[MainWindow] InitializeComponent...");
            InitializeComponent();
            Console.WriteLine("[MainWindow] OK");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[MainWindow] FEHLER in InitializeComponent:");
            Console.WriteLine(ex);
            Console.ResetColor();
            throw;
        }
    }
}
