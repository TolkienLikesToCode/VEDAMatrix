using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VedaMatrix.Models;
using VedaMatrix.Services;

namespace VedaMatrix.Views;

public partial class PauseDialog : Window
{
    public PauseEntry? Result { get; private set; }

    public PauseDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var startBox = this.FindControl<TextBox>("StartBox");
        var endBox = this.FindControl<TextBox>("EndBox");
        var error = this.FindControl<TextBlock>("ErrorText");

        var start = startBox?.Text?.Trim() ?? "";
        var end = endBox?.Text?.Trim() ?? "";

        if (!MatrixEngine.TryParseTime(start, out var t0) || !MatrixEngine.TryParseTime(end, out var t1))
        {
            if (error != null) error.Text = "Bitte gültige Zeiten (HH:mm) eingeben.";
            return;
        }

        if (t1 <= t0)
        {
            if (error != null) error.Text = "Ende muss nach dem Beginn liegen.";
            return;
        }

        Result = new PauseEntry { Start = MatrixEngine.FormatTime(t0), End = MatrixEngine.FormatTime(t1) };
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(null);
    }
}
