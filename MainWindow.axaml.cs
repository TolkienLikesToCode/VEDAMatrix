using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VedaMatrix.ViewModels;
using VedaMatrix.Views;

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

    private async void OnOpenTemplateDialog(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dialog = new TemplateDialog(vm.ProcedureTemplates, vm.SelectedTemplate);
        var result = await dialog.ShowDialog<Models.Procedure?>(this);

        if (result != null)
            vm.SelectedTemplate = result;
    }
}
